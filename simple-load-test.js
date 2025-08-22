import http from 'k6/http';
import { check, sleep } from 'k6';

// Simple test configuration
export const options = {
    vus: 5,           // 5 virtual users
    duration: '2m',   // Run for 2 minutes
};

const BASE_URL = 'http://localhost:5050/api/Store';

// Simple endpoint list
const endpoints = [
    '/join',
    '/look-around',
    '/served',
    '/leave',
    '/simulate',
    '/error'
];

export default function () {
    // Pick a random endpoint
    const endpoint = endpoints[Math.floor(Math.random() * endpoints.length)];
    const url = `${BASE_URL}${endpoint}`;

    let response;
    if (endpoint === '/simulate') {
        response = http.post(url);
    } else {
        response = http.get(url);
    }

    // Basic checks
    check(response, {
        'status is 200': (r) => r.status === 200,
        'response time OK': (r) => r.timings.duration < 1000,
    });

    // Random sleep between 0.5 and 2 seconds
    sleep(Math.random() * 1.5 + 0.5);
}
