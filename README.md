# From Monolith to Microservices: Cloud-Native Migration with Kubernetes

This repository contains the Kubernetes manifests, configurations, and deployment strategies for migrating the **Aircraft Rental Marketplace** from a C# backend monolith and Vue frontend into a secure, orchestrated, and event-driven microservices architecture. 

The deployment is targeted at a 3-node `kubeadm` cluster hosted on an OpenNebula IaaS substrate, running a resource-optimized environment.

---

## System Architecture Overview

The system consists of the following isolated namespace components:
* **`ns-frontend`**: Hosts the containerized Vue SPA served by a hardened non-root Nginx instance.
* **`ns-users`**: Manages user identities, authentication, and company mappings.
* **`ns-fleet`**: Manages aircraft listing, airport directories, and maintenance records.
* **`ns-booking`**: Coordinates flight bookings, payments, and reviews.
* **`ns-infra`**: Contains shared persistent services (PostgreSQL 16 and RabbitMQ 3-Management).
* **`ns-registry`**: Hosts an in-cluster private Docker Registry v2.
* **`ns-gateway`** (`ingress-nginx`): The sole secure entry point for inbound external traffic.
* **`ns-ci`**: Hosts the self-hosted GitHub Actions runner deploying workloads securely via scoped RBAC.

---

## Step-by-Step Deployment Guide

Follow these phases sequentially to initialize, build, deploy, and verify the platform.

---

### Phase 1: Host & Container Runtime Configurations (Run on Nodes)

To ensure the worker nodes can pull images from the in-cluster registry over HTTP without DNS or TLS failures, apply these host-level configurations.

#### 1. Disable IPv6 on both Worker Nodes (`k8s-w1` and `k8s-w2`)
Unrouted public IPv6 traffic can cause public registry pulls (like `quay.io` for cert-manager) to loop back locally and fail. Disable IPv6 routing at the kernel level on both worker nodes:
```bash
sudo sysctl -w net.ipv6.conf.all.disable_ipv6=1
sudo sysctl -w net.ipv6.conf.default.disable_ipv6=1
sudo sysctl -w net.ipv6.conf.lo.disable_ipv6=1
```

#### 2. Map Registry DNS in `/etc/hosts` (On `k8s-w1` and `k8s-w2`)
Allow the hosts to resolve the cluster-internal registry address. Map the registry service's **Cluster-IP** (retrieve using `kubectl get svc -n ns-registry registry` on the control plane) inside the worker nodes' hosts file:
```bash
# Add this line to /etc/hosts (Replace <REGISTRY_CLUSTER_IP> with your actual IP)
<REGISTRY_CLUSTER_IP> registry.ns-registry.svc.cluster.local
```

#### 3. Configure Containerd for HTTP Registry Pulls (On `k8s-w1` and `k8s-w2`)
Modern `containerd` installations manage registry configurations via an isolated directory structure.

1. Correct the `config_path` directive in `/etc/containerd/config.toml` to a single directory string:
   ```toml
   [plugins.'io.containerd.cri.v1.images'.registry]
     config_path = '/etc/containerd/certs.d'
   ```
2. Create the configuration directory matching the registry endpoint:
   ```bash
   sudo mkdir -p /etc/containerd/certs.d/registry.ns-registry.svc.cluster.local:5000
   ```
3. Create a `hosts.toml` file inside that directory:
   ```bash
   sudo nano /etc/containerd/certs.d/registry.ns-registry.svc.cluster.local:5000/hosts.toml
   ```
   Add the following content:
   ```toml
   server = "http://registry.ns-registry.svc.cluster.local:5000"

   [host."http://registry.ns-registry.svc.cluster.local:5000"]
   ```
4. Restart `containerd`:
   ```bash
   sudo systemctl restart containerd
   ```

---

### Phase 2: Deploying Base Infrastructure (`ns-registry` & `ns-infra`)

Deploy the core network spaces, local registry, databases, and message queues.

#### 1. Create the Namespaces (On `k8s-cp`)
```bash
kubectl apply -f k8s/base/namespaces.yaml
```

#### 2. Provision Persistent Volumes (On `k8s-cp`)
To resolve PVC bindings on clusters without a dynamic storage class, create manual Persistent Volumes mapped to host storage. Ensure `/mnt/data/postgres` and `/mnt/data/rabbitmq` directories are created with `777` permissions on your worker nodes, then apply your PV manifests:
```bash
kubectl apply -f k8s/registry/pvc.yaml
kubectl apply -f k8s/infra/postgres.yaml
kubectl apply -f k8s/infra/rabbitmq.yaml
```

#### 3. Stand up the Private Registry (On `k8s-cp`)
```bash
kubectl apply -k k8s/registry
```

#### 4. Stand up the Database and RabbitMQ Broker (On `k8s-cp`)
```bash
kubectl apply -k k8s/infra
```

---

### Phase 3: Building and Pushing Workload Images

With the in-cluster registry running, build your microservices and push them to the cluster.

#### 1. Forward the Registry Port Locally (On `k8s-cp`)
```bash
kubectl port-forward svc/registry -n ns-registry 5000:5000 --address=127.0.0.1 > /dev/null 2>&1 &
docker login localhost:5000 -u ciuser -p ciuser
```

#### 2. Build and Push the Microservices (From your repository root)
Ensure your build context is correctly set to include your code paths.

* **Users Service**:
  ```bash
  docker build -t localhost:5000/users-service:latest -f Services/Users.WebHost/Dockerfile .
  docker push localhost:5000/users-service:latest
  ```
* **Fleet Service**:
  ```bash
  docker build -t localhost:5000/fleet-service:latest -f Services/Fleet.WebHost/Dockerfile .
  docker push localhost:5000/fleet-service:latest
  ```
* **Booking Service**:
  ```bash
  docker build -t localhost:5000/booking-service:latest -f Services/Booking.WebHost/Dockerfile .
  docker push localhost:5000/booking-service:latest
  ```

#### 3. Build the Vue Frontend with Secure NodePort API Enpoints
Because Vite environment variables compile statically at build-time, you must pass the secure HTTPS NodePort (`31857`) URLs as build arguments:
```bash
docker build --no-cache \
  --build-arg VITE_USERS_URL=https://users.aircraft.localtest.me:31857 \
  --build-arg VITE_FLEET_URL=https://fleet.aircraft.localtest.me:31857 \
  --build-arg VITE_BOOKING_URL=https://booking.aircraft.localtest.me:31857 \
  -t localhost:5000/vue-frontend:k8s-http -f frontend_vue/Dockerfile .

docker push localhost:5000/vue-frontend:k8s-http
```

---

### Phase 4: Deploying Workloads (Lab Overlay)

Deploy the services and Vue frontend using the resource-optimized `lab` overlay.

#### 1. Deploy the Application Stack (On `k8s-cp`)
```bash
kubectl apply -k k8s/overlays/opennebula/lab
```

#### 2. Verify Schema Migrations & Running Pods
Monitor the resources. The schema migration jobs should run to completion before workload pods start running:
```bash
# Watch jobs and pods
kubectl get pods -A -w

# Expected output:
# ns-users      users-migrate-xxxxx       0/1     Completed
# ns-booking    booking-migrate-xxxxx     0/1     Completed
# ns-users      users-service-xxxxx       1/1     Running
```

---

### Phase 5: Local SSL Verification with `mkcert` (The "No Warnings" Fix)

To allow your browser to trust the secure subdomains natively without certificate bypass warnings (the "whitelist trick"), generate a locally-trusted CA.

#### 1. Generate Wildcard Certificates (On your local personal computer)
1. Install `mkcert` and NSS support:
   ```bash
   sudo dnf install mkcert nss-tools -y  # Fedora
   mkcert -install
   ```
2. Generate wildcard certificates:
   ```bash
   mkcert "*.aircraft.localtest.me" "aircraft.localtest.me"
   ```
   *This outputs two files: `_wildcard.aircraft.localtest.me+1.pem` (certificate) and `_wildcard.aircraft.localtest.me+1-key.pem` (private key).*

#### 2. Copy Certificate Content to `k8s-cp`
Copy the text content of these two files and save them on `k8s-cp` as `/tmp/cert.pem` and `/tmp/key.pem` respectively.

#### 3. Inject Manual Trusted Secrets (On `k8s-cp`)
Replace the default ingress TLS placeholders with your trusted `mkcert` credentials:
```bash
# Delete default secrets
kubectl delete secret frontend-tls -n ns-frontend
kubectl delete secret users-tls -n ns-users
kubectl delete secret fleet-tls -n ns-fleet
kubectl delete secret booking-tls -n ns-booking

# Create trusted manual secrets
kubectl create secret tls frontend-tls --cert=/tmp/cert.pem --key=/tmp/key.pem -n ns-frontend
kubectl create secret tls users-tls --cert=/tmp/cert.pem --key=/tmp/key.pem -n ns-users
kubectl create secret tls fleet-tls --cert=/tmp/cert.pem --key=/tmp/key.pem -n ns-fleet
kubectl create secret tls booking-tls --cert=/tmp/cert.pem --key=/tmp/key.pem -n ns-booking
```

#### 4. Configure Double SSH Port Forwarding
Since the OpenNebula private network sits behind the Azure VM Host, configure a double port-forwarding tunnel.

1. **Inner Tunnel (Run on the Azure VM Host as `oneadmin`):**
   ```bash
   ssh -f -N -L 127.0.0.1:31857:172.16.100.11:31857 -L 127.0.0.1:32629:172.16.100.11:32629 root@172.16.100.11
   ```
2. **Outer Tunnel (Run on your Local Personal Computer):**
   ```bash
   ssh -L 31857:127.0.0.1:31857 -L 32629:127.0.0.1:32629 <AZURE_USER>@<AZURE_VM_PUBLIC_IP>
   ```
3. **Local Hosts Mapping (On your Local Personal Computer):**
   Append these domains to your local `/etc/hosts` file:
   ```text
   127.0.0.1 app.aircraft.localtest.me
   127.0.0.1 users.aircraft.localtest.me
   127.0.0.1 fleet.aircraft.localtest.me
   127.0.0.1 booking.aircraft.localtest.me
   ```

#### 5. Open Your Browser
Navigate to:
👉 **`https://app.aircraft.localtest.me:31857`**

The entire application will now load securely over HTTPS with a **valid green padlock** and no warning screens.

---

## Updating the Self-Hosted GitHub Actions Runner Image

The GitHub Actions runner image is stored in the in-cluster registry (`ns-registry`).
After rebuilding the runner image, push it to the registry and restart the runner deployment.

### 1. Configure Docker to Trust the Local Registry

```bash
cat > /etc/docker/daemon.json <<'EOF'
{
  "insecure-registries": ["registry.ns-registry.svc.cluster.local:5000"]
}
EOF

systemctl reload docker || systemctl restart docker
```

### 2. Push the Runner Image to the Registry

```bash
# Ensure port 5000 is free
fuser -k 5000/tcp 2>/dev/null || true

# Expose the registry locally
kubectl -n ns-registry port-forward svc/registry 5000:5000 &
PF_PID=$!

sleep 2

# Authenticate to the registry
docker login registry.ns-registry.svc.cluster.local:5000 \
  --username ciuser \
  --password ciuser

# Push the image
docker push registry.ns-registry.svc.cluster.local:5000/github-runner:latest

# Close the port-forward
kill $PF_PID
```

### 3. Restart the Runner Deployment

```bash
kubectl -n ns-ci rollout restart deployment/github-runner

kubectl -n ns-ci rollout status deployment/github-runner \
  --timeout=120s
```

### 4. Verify the Runner Image

```bash
kubectl -n ns-ci exec deployment/github-runner \
  -c runner \
  -- kubectl version --client
```

Expected output:

```text
Client Version: vX.Y.Z
```