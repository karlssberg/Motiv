// PROTOTYPE — throwaway.
export const SAMPLE =
  '{\n  "customerId": "cust-42",\n  "age": 30,\n  "isActive": true,\n  "orderCount": 3,\n  "orders": [{ "total": 120 }]\n}';

export const SCENARIOS = [
  { name: 'Active adult, 3 orders', json: SAMPLE },
  { name: 'Minor', json: '{\n  "customerId": "cust-7",\n  "age": 16,\n  "isActive": true,\n  "orderCount": 1,\n  "orders": [{ "total": 20 }]\n}' },
  { name: 'Dormant account', json: '{\n  "customerId": "cust-9",\n  "age": 41,\n  "isActive": false,\n  "orderCount": 12,\n  "orders": [{ "total": 300 }]\n}' },
  { name: 'New, no orders', json: '{\n  "customerId": "cust-1",\n  "age": 25,\n  "isActive": true,\n  "orderCount": 0\n}' },
];
