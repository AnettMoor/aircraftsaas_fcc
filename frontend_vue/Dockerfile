# =====================================================================
# Vue SPA — hardened multi-stage Dockerfile
#
#   * Build stage: node:20-alpine (SDK kept OUT of final image).
#   * Runtime stage: nginx:alpine, but reconfigured so nginx itself runs
#     as a non-root user (UID 1000). The stock nginx image runs the
#     master process as root because it binds to :80; we bind to :8080
#     instead (unprivileged) and chown every path nginx writes to.
#   * USER 1000:1000 is explicit so `runAsNonRoot: true` enforced by
#     the Kubernetes PodSecurityContext passes its admission check.
#   * No HEALTHCHECK directive — Kubernetes probes own liveness.
# =====================================================================

# --- Stage 1: Build the Vue application --------------------------------
FROM node:20-alpine AS build
WORKDIR /app

# Restore dependency layer first so it's cached as long as the lockfile
# is unchanged.
COPY package.json package-lock.json ./
RUN npm ci

# Copy the rest of the source and produce the static bundle.
COPY . .
RUN npm run build

# --- Stage 2: Serve with a hardened, non-root nginx --------------------
FROM nginx:alpine AS production

# Drop the default site config; ours listens on 8080 (unprivileged) and
# only serves static assets — all /api/* traffic goes via the Ingress.
RUN rm /etc/nginx/conf.d/default.conf

# Copy the custom nginx config and the built SPA assets.
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

# Make every path nginx writes to (cache, run, logs, the served webroot)
# writable by UID 1000. Then rewrite the master config to:
#   * drop `user nginx;` (only the master can switch user, and the master
#     here is already UID 1000 — leaving the directive in place would
#     fail with "getpwnam("nginx") failed");
#   * relocate the pid file out of /var/run (which is root-owned on
#     some base images) into a path we own.
RUN set -eux ; \
    mkdir -p /var/cache/nginx /var/run /var/log/nginx /tmp/nginx ; \
    chown -R 1000:1000 /var/cache/nginx /var/run /var/log/nginx \
                       /usr/share/nginx/html /etc/nginx/conf.d /tmp/nginx ; \
    sed -i 's|^user  nginx;|# user directive removed: container already runs as UID 1000|' /etc/nginx/nginx.conf ; \
    sed -i 's|/var/run/nginx.pid|/tmp/nginx/nginx.pid|'                                    /etc/nginx/nginx.conf

# Run as the unprivileged user. Kubernetes PodSecurityContext enforces
# runAsNonRoot:true; this directive lets that admission check pass.
USER 1000:1000

# Bind to an unprivileged port (>= 1024). The Kubernetes Service in
# k8s/frontend/service.yaml maps Service :80 -> containerPort :8080.
EXPOSE 8080

# Disable Docker HEALTHCHECK — Kubernetes probes own liveness.
HEALTHCHECK NONE

CMD ["nginx", "-g", "daemon off;"]
