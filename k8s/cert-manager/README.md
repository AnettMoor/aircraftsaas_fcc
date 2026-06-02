# cert-manager — Phase C §C.15

Provides TLS termination at the Ingress for every public host (Vue SPA +
the three backend subdomains). All certificates are issued by Let's
Encrypt on the OpenNebula cluster.

| Overlay | Issuer | Trust |
|---|---|---|
| `overlays/opennebula`   | `letsencrypt-staging` (HTTP-01)     | Public trust, rate-limited; switch to `letsencrypt-prod` once the edge DNS is stable. |

The cert-manager controller + CRDs ship via [`controller.yaml`](controller.yaml).
The production ClusterIssuer ships in
[`issuer-letsencrypt.yaml`](issuer-letsencrypt.yaml).

Per-namespace `Certificate` objects are NOT committed: the Ingress
resources carry the `cert-manager.io/cluster-issuer` annotation, which
triggers cert-manager to issue + rotate automatically into a Secret
named in the Ingress's `spec.tls[*].secretName`.

## Security headers

The Phase C ingress changes add the following annotations to every
public Ingress:

* `nginx.ingress.kubernetes.io/ssl-redirect: "true"` — force HTTPS.
* `nginx.ingress.kubernetes.io/hsts: "true"` + `hsts-max-age=31536000`.
* `nginx.ingress.kubernetes.io/configuration-snippet:` adds
  `X-Frame-Options DENY`, `X-Content-Type-Options nosniff`,
  `Referrer-Policy strict-origin-when-cross-origin`, and a
  conservative CSP (`default-src 'self'`).
