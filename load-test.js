import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate } from 'k6/metrics';

// Custom metrics
const errorRate = new Rate('errors');

// Test configuration - optimized for high join traffic with realistic store patterns
export const options = {
    stages: [
        { duration: '1m', target: 100 },   // Quick ramp-up - morning rush
        { duration: '2m', target: 300 },   // Busy period starts
        { duration: '3m', target: 1000 },   // Peak hours - lots of new customers joining
        { duration: '4m', target: 300 },   // Very busy - high join + browse + serve activity
        { duration: '2m', target: 150 },  // Peak rush - maximum join traffic
        { duration: '3m', target: 100 },   // Gradual decline but still busy
        { duration: '1m', target: 0 },    // Closing time
    ],
    thresholds: {
        http_req_duration: ['p(95)<800'], // Slightly relaxed for higher load
        http_req_failed: ['rate<0.15'],   // Allow slightly higher error rate due to load
        errors: ['rate<0.15'],            // Custom error rate threshold
        'http_req_duration{name:join}': ['p(95)<600'],      // Join should be fast
        'http_req_duration{name:leave}': ['p(95)<400'],     // Leave should be very fast
    },
};

// Base URL - adjust this to match your setup
const BASE_URL = 'http://localhost:5050/api/Store';

// Define API endpoints with their weights (probability of being called)
// High join activity, moderate look-around, some served, continuous leave
const endpoints = [
    { path: '/join', method: 'GET', weight: 45, name: 'join' },        // Heavy join traffic
    { path: '/look-around', method: 'GET', weight: 25, name: 'look_around' }, // Moderate browsing
    { path: '/served', method: 'GET', weight: 15, name: 'served' },    // Some purchases
    { path: '/leave', method: 'GET', weight: 12, name: 'leave' },      // Continuous exits
    { path: '/error', method: 'GET', weight: 3, name: 'error' },       // Occasional errors
];

// User behavior scenarios - focused on realistic store traffic patterns
const userScenarios = [
    'new_visitor',        // Heavy join traffic
    'browsing_customer',  // Join + multiple look-arounds
    'buying_customer',    // Full journey: join -> look -> served -> leave
    'quick_browser',      // Join -> brief look -> leave
    'leaving_customer'    // Focus on leaving (people who joined earlier)
];

// Weighted random selection function
function getRandomEndpoint() {
    const totalWeight = endpoints.reduce((sum, ep) => sum + ep.weight, 0);
    let random = Math.random() * totalWeight;

    for (const endpoint of endpoints) {
        random -= endpoint.weight;
        if (random <= 0) {
            return endpoint;
        }
    }
    return endpoints[0]; // fallback
}

// User behavior simulation - realistic store traffic patterns
function simulateUserJourney() {
    const scenario = userScenarios[Math.floor(Math.random() * userScenarios.length)];

    switch (scenario) {
        case 'new_visitor':
            // High frequency new visitors joining - peak traffic
            makeRequest('/join', 'GET');
            sleep(Math.random() * 0.5 + 0.2); // Quick join, short pause

            // 40% chance to immediately look around after joining
            if (Math.random() < 0.4) {
                makeRequest('/look-around', 'GET');
            }
            break;

        case 'browsing_customer':
            // Customer joins and browses extensively
            makeRequest('/join', 'GET');
            sleep(Math.random() * 1 + 0.5);

            // Browse multiple times (2-5 look-arounds)
            const browseCount = Math.floor(Math.random() * 4) + 2;
            for (let i = 0; i < browseCount; i++) {
                makeRequest('/look-around', 'GET');
                sleep(Math.random() * 2 + 0.5); // Browsing time
            }

            // 60% chance to eventually leave
            if (Math.random() < 0.6) {
                makeRequest('/leave', 'GET');
            }
            break;

        case 'buying_customer':
            // Complete customer journey: join -> browse -> buy -> leave
            makeRequest('/join', 'GET');
            sleep(Math.random() * 1 + 0.3);

            // Browse 1-3 times before buying
            const lookCount = Math.floor(Math.random() * 3) + 1;
            for (let i = 0; i < lookCount; i++) {
                makeRequest('/look-around', 'GET');
                sleep(Math.random() * 1.5 + 0.5);
            }

            // Get served
            makeRequest('/served', 'GET');
            sleep(Math.random() * 1 + 0.5);

            break;

        case 'quick_browser':
            // Fast interaction: join -> quick look -> leave
            makeRequest('/join', 'GET');
            sleep(Math.random() * 0.5 + 0.2);

            makeRequest('/look-around', 'GET');
            sleep(Math.random() * 0.8 + 0.3);

            makeRequest('/leave', 'GET');
            break;

        case 'leaving_customer':
            // Focus on customers leaving (simulates people who joined earlier)
            makeRequest('/leave', 'GET');
            sleep(Math.random() * 0.5 + 0.1);

            // Small chance they encountered an error before leaving
            if (Math.random() < 0.15) {
                makeRequest('/error', 'GET');
            }
            break;
    }
}

// Make HTTP request with error handling
function makeRequest(path, method = 'GET') {
    const url = `${BASE_URL}${path}`;
    let response;

    const params = {
        headers: {
            'Content-Type': 'application/json',
            'User-Agent': 'k6-load-test/1.0',
        },
        timeout: '30s',
    };

    try {
        if (method === 'POST') {
            response = http.post(url, null, params);
        } else {
            response = http.get(url, params);
        }

        // Check response
        const success = check(response, {
            'status is 200': (r) => r.status === 200,
            'response time < 500ms': (r) => r.timings.duration < 500,
            'response body exists': (r) => r.body && r.body.length > 0,
        });

        if (!success) {
            errorRate.add(1);
            console.error(`Request failed: ${method} ${url} - Status: ${response.status}`);
        } else {
            errorRate.add(0);
        }

    } catch (error) {
        errorRate.add(1);
        console.error(`Request error: ${method} ${url} - Error: ${error.message}`);
    }

    return response;
}

// Main test function - runs for each virtual user
export default function () {
    // 80% chance to follow a realistic user journey, 20% chance for random high-frequency requests
    if (Math.random() < 0.8) {
        simulateUserJourney();
    } else {
        // Make 2-6 quick random requests (higher frequency, shorter pauses)
        const requestCount = Math.floor(Math.random() * 5) + 2;

        for (let i = 0; i < requestCount; i++) {
            const endpoint = getRandomEndpoint();
            makeRequest(endpoint.path, endpoint.method);

            // Shorter think time for high-frequency traffic (0.2 to 1.5 seconds)
            sleep(Math.random() * 1.3 + 0.2);
        }
    }

    // Shorter pause between iterations for more traffic (0.5 to 2 seconds)
    sleep(Math.random() * 1.5 + 0.5);
}

// Setup function - runs once before the test
export function setup() {
    console.log('Starting high-traffic store simulation...');
    console.log(`Target URL: ${BASE_URL}`);
    console.log('Traffic pattern - simulating busy store:');
    console.log('  1m ramp-up to 15 users (morning opening)');
    console.log('  2m steady at 30 users (getting busy)');
    console.log('  3m ramp to 60 users (peak hours start)');
    console.log('  4m steady at 80 users (very busy period)');
    console.log('  2m peak at 120 users (rush hour - high joins!)');
    console.log('  3m ramp-down to 60 users (still busy)');
    console.log('  2m ramp-down to 30 users (afternoon lull)');
    console.log('  1m ramp-down to 0 users (closing)');
    console.log('');
    console.log('Expected traffic distribution:');
    console.log('  - 45% JOIN requests (high new customer traffic)');
    console.log('  - 25% LOOK-AROUND requests (browsing)');
    console.log('  - 15% SERVED requests (purchases)');
    console.log('  - 12% LEAVE requests (continuous exits)');
    console.log('  - 3% ERROR requests (occasional issues)');
}

// Teardown function - runs once after the test
export function teardown(data) {
    console.log('Load test completed!');
}
