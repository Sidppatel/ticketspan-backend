// k6 scenario: booking creation + Stripe test-mode PaymentIntent confirm.
// 20 VUs for 5 min. Requires Stripe test-mode secret on backend and a seeded
// test user with magic-link login flow bypass (E2E_TEST_USER / E2E_TEST_TOKEN).
//
// Run:  k6 run scenarios/checkout.js
// Env:
//   BASE_URL            (default http://localhost:8000)
//   E2E_TEST_USER       email of seeded load-test user (required)
//   E2E_TEST_TOKEN      dev-login token or magic-link token for that user (required)
//   EVENT_ID            UUID of a seeded event with open capacity (required)
//   TICKET_TYPE_ID      UUID of a ticket type on that event (required)
//
// NOTE: this scenario is expected to be run against a staging environment
// seeded with test fixtures. Against local dev it will likely 401/404 unless
// you prepare the same fixtures. Do NOT point this at production.

import http from 'k6/http';
import { check, sleep, fail } from 'k6';
import { Trend, Counter } from 'k6/metrics';

const BASE_URL        = __ENV.BASE_URL        || 'http://localhost:8000';
const TEST_USER       = __ENV.E2E_TEST_USER   || '';
const TEST_TOKEN      = __ENV.E2E_TEST_TOKEN  || '';
const EVENT_ID        = __ENV.EVENT_ID        || '';
const TICKET_TYPE_ID  = __ENV.TICKET_TYPE_ID  || '';

export const options = {
    scenarios: {
        checkout: {
            executor: 'constant-vus',
            vus: 20,
            duration: '5m',
        },
    },
    thresholds: {
        http_req_duration: ['p(95)<1500'],
        http_req_failed: ['rate<0.02'],
        checks: ['rate>0.98'],
    },
};

const quoteLatency    = new Trend('ep_quote_ms',    true);
const bookingLatency = new Trend('ep_booking_ms', true);
const confirmLatency  = new Trend('ep_confirm_ms',  true);
const bookingsOk     = new Counter('ep_bookings_ok');
const bookingsFail   = new Counter('ep_bookings_fail');

export function setup() {
    if (!TEST_USER || !TEST_TOKEN || !EVENT_ID || !TICKET_TYPE_ID) {
        fail('Missing required env: E2E_TEST_USER, E2E_TEST_TOKEN, EVENT_ID, TICKET_TYPE_ID');
    }
    // Exchange token for session cookie via dev-login endpoint.
    const loginRes = http.post(`${BASE_URL}/api/v1/auth/dev-login`,
        JSON.stringify({ email: TEST_USER, token: TEST_TOKEN }),
        { headers: { 'Content-Type': 'application/json', 'X-Portal': 'user' } });
    check(loginRes, { 'login 200': (r) => r.status === 200 }) || fail('login failed');
    // k6 automatically manages cookies per-VU via the default cookie jar.
    const setCookie = loginRes.headers['Set-Cookie'] || loginRes.headers['set-cookie'];
    return { cookie: setCookie };
}

export default function (data) {
    const headers = {
        'Content-Type': 'application/json',
        'X-Portal': 'user',
        Cookie: data.cookie || '',
    };

    const quotePayload = JSON.stringify({
        eventId: EVENT_ID,
        items: [{ ticketTypeId: TICKET_TYPE_ID, quantity: 1 }],
    });
    const quoteRes = http.post(`${BASE_URL}/api/v1/bookings/quote`, quotePayload,
        { headers, tags: { name: 'quote' } });
    quoteLatency.add(quoteRes.timings.duration);
    if (!check(quoteRes, { 'quote 200': (r) => r.status === 200 })) {
        bookingsFail.add(1);
        return;
    }

    const bookingRes = http.post(`${BASE_URL}/api/v1/bookings`, quotePayload,
        { headers, tags: { name: 'booking_create' } });
    bookingLatency.add(bookingRes.timings.duration);
    if (!check(bookingRes, { 'booking 200/201': (r) => r.status === 200 || r.status === 201 })) {
        bookingsFail.add(1);
        return;
    }

    let bookingId, clientSecret;
    try {
        const body = bookingRes.json();
        bookingId   = body.bookingId   || body.id;
        clientSecret = body.clientSecret || body.stripeClientSecret;
    } catch {
        bookingsFail.add(1);
        return;
    }

    // In a real staging run, the client-side would use Stripe.js confirmCardPayment
    // with test card 4242... and then call /bookings/{id}/confirm. k6 can't drive the
    // Stripe.js SDK, so this is a server-confirm call with an assumed-succeeded intent
    // (requires backend configured with STRIPE_AUTO_CONFIRM_TEST=true in the target env).
    if (bookingId) {
        const confirmRes = http.post(`${BASE_URL}/api/v1/bookings/${bookingId}/confirm`,
            JSON.stringify({ clientSecret }),
            { headers, tags: { name: 'booking_confirm' } });
        confirmLatency.add(confirmRes.timings.duration);
        if (check(confirmRes, { 'confirm 200': (r) => r.status === 200 })) {
            bookingsOk.add(1);
        } else {
            bookingsFail.add(1);
        }
    }

    sleep(2);
}
