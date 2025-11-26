# IIS Access Log Review (2025-11-26)

## Overview
The provided IIS access log excerpt for 2025-11-26 shows a large volume of 404 responses alongside successful authentication and API traffic. Several requests are characteristic of automated scanning and exploit probing. Legitimate application interactions are also visible for feedback submissions, service worker fetches, and admin dashboard access.

## Notable Traffic Patterns
- **Automated scanning and reconnaissance**
  - Masscan-style probe with the `ivre-masscan` user agent requesting the site root and receiving 404.【F:docs/log-analysis-2025-11-26.md†L9-L11】
  - Multiple generic root and `index.html` requests from various IPs and user agents (including Palo Alto Networks, Censys, and InternetMeasurement bots) resulting in 404s, indicating broad reconnaissance rather than normal user navigation.【F:docs/log-analysis-2025-11-26.md†L12-L17】
  - Attempts to fetch sensitive files such as `/.git/config` and `/.env`, plus PHP upload and admin paths (`upl.php`, `password.php`, `systembc/password.php`), consistent with commodity exploit scans.【F:docs/log-analysis-2025-11-26.md†L18-L22】
  - Extensive probing for PHPUnit `eval-stdin.php` across numerous directory permutations, likely looking for a known remote code execution vector.【F:docs/log-analysis-2025-11-26.md†L23-L27】
  - Requests targeting ThinkPHP invocation parameters and Docker metadata (`/containers/json`), reflecting exploit kit fingerprints rather than expected application usage.【F:docs/log-analysis-2025-11-26.md†L28-L30】
- **Bots fetching `robots.txt` and `.well-known/security.txt`**
  - OAI SearchBot and CensysInspect attempted to retrieve `robots.txt` and `security.txt`, receiving 404 responses.【F:docs/log-analysis-2025-11-26.md†L32-L34】
- **Application interactions**
  - Repeated `POST /api/feedback` requests returning 201 indicate active client usage, likely from the Liberty ClientFlow frontend referenced in the referrer header.【F:docs/log-analysis-2025-11-26.md†L36-L38】
  - Service worker `sw.js`, kiosk dashboard assets, and staff listing fetches show routine PWA activity from the same client context.【F:docs/log-analysis-2025-11-26.md†L39-L41】
  - A successful `POST /api/users/login` followed by admin dashboard and branch data retrieval demonstrates legitimate administrative access after an initial 401 challenge.【F:docs/log-analysis-2025-11-26.md†L42-L45】

## Risk Assessment
- The exploit probes (PHPUnit, ThinkPHP, environment files) are high signal for opportunistic attacks and warrant defensive hardening even though all responses returned 404.
- Absence of `robots.txt` and `security.txt` leads to repeated 404s from well-behaved crawlers; adding these files can reduce noise and improve vulnerability disclosure hygiene.
- No evidence in this excerpt of successful exploitation; however, the breadth of probing underscores the need for continuous monitoring and alerting.

## Recommended Mitigations
1. **WAF or reverse-proxy filtering**: Block common exploit signatures (PHPUnit `eval-stdin.php`, ThinkPHP function invocation, `.env` access) and throttle repeated 404s from the same IPs to reduce log noise and attack surface.
2. **Honeypot and rate limiting**: Implement rate limits on anonymous requests to `/` and `index.html`, and consider tarpit responses for known scanner user agents (e.g., `masscan`, `libredtail-http`).
3. **Security headers and hardening**: Confirm directory browsing is disabled and that any PHP-related handlers are removed if unused to prevent unexpected script execution paths.
4. **Add crawler hint files**: Publish minimal `robots.txt` and `.well-known/security.txt` to communicate scanning boundaries and disclosure contacts, reducing benign 404 noise and improving security posture.
5. **Authentication monitoring**: Alert on unusual login patterns or multiple failed authentications; ensure multi-factor authentication is enabled for admin users.
6. **Logging and alerting**: Centralize log ingestion with alert rules for exploit patterns seen here to enable rapid triage.

## Key Indicators to Monitor
- Paths: `/.env`, `/.git/config`, `/vendor/phpunit/phpunit/src/Util/PHP/eval-stdin.php`, `/index.php` with `think\app\invokefunction`, `/containers/json`.
- User agents: `ivre-masscan`, `libredtail-http`, `InternetMeasurement/1.0`, `CensysInspect/1.1`, `Hello from Palo Alto Networks`.
- Status codes: spikes of 404/301 on root and swagger endpoints outside expected admin maintenance windows.

