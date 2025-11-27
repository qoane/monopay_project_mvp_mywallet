# MyWallet Local Testing & `invalid_token` Troubleshooting

Use these steps to run the API locally and avoid the `WWW-Authenticate: Bearer error="invalid_token"` response when exercising MyWallet endpoints.

## 1. Start the API with predictable settings
- Set `UseInMemoryDatabase` to `true` in `api/appsettings.json` if you do not have SQL Server available.
- Run the service with the built-in path base: `dotnet run --project api --urls http://localhost:5000`.
- Swagger UI will be at `http://localhost:5000/api/swagger` because `PathBase` is `/api`.

## 2. Create a verified user and capture the JWT
1. Register: `POST /api/v1/users/register` with your email/phone/password.
2. Copy the verification token printed to the API console (used when SMTP is not configured).
3. Verify: `GET /api/v1/users/verify?token=<copied_token>`.
4. Log in: `POST /api/v1/users/login` and save the `token` value from the response.

## 3. Call MyWallet with the bearer token
- In Swagger, click **Authorize** and paste `Bearer <token>`, or add the header manually in your client.
- Use `POST /api/v1/payments` with a body similar to:
  ```json
  {
    "paymentMethod": "mywallet",
    "amount": 10.00,
    "currency": "LSL",
    "merchantId": "<your_merchant_id>",
    "customer": { "phone": "<recipient_msisdn>" },
    "reference": "test-ref-001",
    "otp": "99999"
  }
  ```
- Poll `GET /api/v1/payments/{id}` to track status.

## 4. Common causes of `invalid_token`
- **Missing `Authorization` header**: the API always replies with `Bearer error="invalid_token"` when no JWT is provided.
- **Token expired**: tokens last `DurationInMinutes` (default 60) from `Jwt` settings; log in again if an hour has passed.
- **Wrong host or path**: requests must hit the same base you started (e.g., `http://localhost:5000/api/...`). Using `https://localhost:44318` or omitting `/api` can route to another site that rejects the token.
- **Mismatched secrets**: if you run multiple API instances, ensure they share the same `Jwt:Key`, `Issuer`, and `Audience`, or tokens from one instance will be invalid on another.

Following this checklist should keep MyWallet calls authorized and runnable on the local environment.
