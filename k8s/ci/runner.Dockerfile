# =====================================================================
# Custom GitHub Actions self-hosted runner image.
#
# Extends myoung34/github-runner (Ubuntu-based) with:
#   * kubectl  — installed from the official Kubernetes APT repo so the
#                deploy jobs (rollout + smoke) can call `kubectl set image`
#                and `kubectl rollout status` without a runtime download.
#   * curl, jq — kept here explicitly so the image is self-contained even
#                if ADDITIONAL_PACKAGES is cleared in the Deployment.
#
# The Kubernetes APT repo pin tracks the stable minor release channel.
# Bump the channel path (v1.30 → v1.31 etc.) when you upgrade the cluster.
# =====================================================================
FROM myoung34/github-runner:latest

USER root

RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        apt-transport-https \
        ca-certificates \
        curl \
        gnupg \
        jq && \
    # Add the Kubernetes APT signing key and repo.
    curl -fsSL https://pkgs.k8s.io/core:/stable:/v1.30/deb/Release.key \
        | gpg --dearmor -o /etc/apt/keyrings/kubernetes-apt-keyring.gpg && \
    chmod 644 /etc/apt/keyrings/kubernetes-apt-keyring.gpg && \
    echo 'deb [signed-by=/etc/apt/keyrings/kubernetes-apt-keyring.gpg] https://pkgs.k8s.io/core:/stable:/v1.30/deb/ /' \
        > /etc/apt/sources.list.d/kubernetes.list && \
    apt-get update && \
    apt-get install -y --no-install-recommends kubectl && \
    # Verify the binary is present and executable.
    kubectl version --client && \
    # Clean up to keep the layer small.
    rm -rf /var/lib/apt/lists/*

# Return to the non-root runner user that the base image expects.
USER runner
