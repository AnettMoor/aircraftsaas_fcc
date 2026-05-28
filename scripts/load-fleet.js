import http from 'k6/http';
import { sleep } from 'k6';

export const options = {
  stages: [
    { duration: '30s', target: 50 },
    { duration: '2m',  target: 300 },
    { duration: '1m',  target: 0 },
  ],
};

export default function () {
  http.get('http://aircraft.localtest.me/api/fleet/Aircraft');
  sleep(0.1);
}
