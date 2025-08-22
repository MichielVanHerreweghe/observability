-- Redis Lua script to export application metrics in Prometheus format
local function get_metric_value(key)
    local value = redis.call('GET', key)
    if value then
        local data = cjson.decode(value)
        return data
    end
    return nil
end

local function parse_metric_key(key)
    local parts = {}
    for part in string.gmatch(key, "([^:]+)") do
        table.insert(parts, part)
    end
    
    if #parts >= 3 then
        return {
            prefix = parts[1],      -- "metrics"
            type = parts[2],        -- "counter", "gauge", "histogram"
            name = parts[3],        -- metric name
            tags = parts[4] or ""   -- tags if present
        }
    end
    return nil
end

local function format_labels(tags)
    if tags == "" then
        return ""
    end
    
    local labels = {}
    for tag_pair in string.gmatch(tags, "([^,]+)") do
        local key, value = string.match(tag_pair, "([^=]+)=([^=]+)")
        if key and value then
            table.insert(labels, key .. '="' .. value .. '"')
        end
    end
    
    if #labels > 0 then
        return "{" .. table.concat(labels, ",") .. "}"
    end
    return ""
end

-- Main export function
local function export_metrics()
    local keys = redis.call('KEYS', 'metrics:*')
    local output = {}
    local metric_types = {}
    
    for _, key in ipairs(keys) do
        local parsed = parse_metric_key(key)
        if parsed then
            local data = get_metric_value(key)
            if data then
                local labels = format_labels(parsed.tags)
                
                -- Add TYPE declaration (only once per metric name)
                if not metric_types[parsed.name] then
                    table.insert(output, "# TYPE " .. parsed.name .. " " .. parsed.type)
                    metric_types[parsed.name] = true
                end
                
                if parsed.type == "counter" or parsed.type == "gauge" then
                    local value = data.value or 0
                    table.insert(output, parsed.name .. labels .. " " .. value)
                    
                elseif parsed.type == "histogram" then
                    local count = data.count or 0
                    local sum = data["sum"] or 0
                    local p50 = data.p50 or 0
                    local p95 = data.p95 or 0
                    local p99 = data.p99 or 0
                    
                    table.insert(output, parsed.name .. "_count" .. labels .. " " .. count)
                    table.insert(output, parsed.name .. "_sum" .. labels .. " " .. sum)
                    
                    -- Histogram buckets
                    local bucket_labels = labels
                    if labels ~= "" then
                        bucket_labels = string.gsub(labels, "}", ',le="0.5"}')
                    else
                        bucket_labels = '{le="0.5"}'
                    end
                    table.insert(output, parsed.name .. "_bucket" .. bucket_labels .. " " .. p50)
                    
                    bucket_labels = labels
                    if labels ~= "" then
                        bucket_labels = string.gsub(labels, "}", ',le="0.95"}')
                    else
                        bucket_labels = '{le="0.95"}'
                    end
                    table.insert(output, parsed.name .. "_bucket" .. bucket_labels .. " " .. p95)
                    
                    bucket_labels = labels
                    if labels ~= "" then
                        bucket_labels = string.gsub(labels, "}", ',le="0.99"}')
                    else
                        bucket_labels = '{le="0.99"}'
                    end
                    table.insert(output, parsed.name .. "_bucket" .. bucket_labels .. " " .. p99)
                    
                    bucket_labels = labels
                    if labels ~= "" then
                        bucket_labels = string.gsub(labels, "}", ',le="+Inf"}')
                    else
                        bucket_labels = '{le="+Inf"}'
                    end
                    table.insert(output, parsed.name .. "_bucket" .. bucket_labels .. " " .. count)
                end
            end
        end
    end
    
    return table.concat(output, "\n") .. "\n"
end

-- Execute the export
return export_metrics()
