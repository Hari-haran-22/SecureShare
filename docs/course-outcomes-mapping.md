# INT378 course-outcome mapping

This record maps the SecureShare implementation to **INT378: Advancements in Cloud and DevOps**. Runtime screenshots and exported Jenkins artifacts should be added to the submitted report; secrets, private keys, Terraform state, and recovery codes must remain outside the report.

| Course area | Repository evidence | Status |
| --- | --- | --- |
| CO1: cloud and security fundamentals | Dedicated VPC, public/private subnets, security groups, encrypted storage, HTTPS, IAM roles | Implemented |
| CO2: scalability | Split application/services nodes, resource limits, metrics, load-test-ready health endpoints | Partial; ALB/Auto Scaling remains a design exercise because local file storage permits one API writer |
| CO3: network security and authentication | Restricted SSH CIDRs, SSM role, IMDSv2, CSRF protection, owner sessions, password-protected links | Implemented |
| CO4: cost and cloud-native optimization | Free-tier-sized instances, optional resources, lifecycle cleanup, AWS Budget, scheduled destruction | Implemented |
| CO5: development and deployment security | Tests, Trivy source/image gates, Jenkins credentials, pinned SSH host keys, non-root containers | Implemented |
| CO6: trends and governance | Terraform/CloudFormation comparison, tags, SBOM/signing identified as future supply-chain work | Partial |

## Practical evidence

| Practical | Evidence or command |
| --- | --- |
| EC2, VPC, security groups and web deployment | `teraform/main.tf`, `terraform plan`, Jenkins deploy stage, HTTPS readiness check |
| IAM, least privilege and RBAC | EC2 trust policy, SSM attachment, scoped S3 backup policy; use IAM Identity Center and MFA for the human operator |
| CloudFormation and Terraform | `infrastructure.yaml`, `terraform plan`; record a CloudFormation change set and rollback separately |
| Docker lifecycle and Docker Hub | `Dockerfile`, Compose files, Jenkins build/scan/push, remote deployment pull |
| Terraform EC2 and S3 | Two EC2 nodes plus optional encrypted/versioned S3 backup bucket; state is inspected after destroy |
| Jenkins and GitHub webhook | `githubPush()` trigger and checkout/build/test/deploy stages in `Jenkinsfile` |
| DevSecOps | Locked restore, automated tests, Trivy filesystem/image gates, archived JSON assessment reports, Jenkins credentials |
| Prometheus and Node Exporter | `prometheus.yml`, `node-exporter` Compose service and host alert rules |
| Grafana | Provisioned SecureShare, .NET and host-infrastructure dashboards |
| Serverless and CloudWatch | Optional scheduled health-check Lambda, seven-day log group and error alarm managed by Terraform |
| Cost optimization | Optional $5 budget, project tags, short-lived infrastructure scripts and `terraform destroy` audit |

## Evidence checklist

Capture the following during the final demonstration:

1. Successful Jenkins stages and archived test/security artifacts.
2. Terraform plan and apply outputs with secrets removed.
3. Running Docker containers and the Docker Hub commit tag.
4. Grafana CPU, memory, disk and network panels plus one safely triggered alert.
5. Lambda invocation result and CloudWatch log event when the optional extension is enabled.
6. AWS Cost Explorer filtered by the `Project=SecureShare` tag.
7. `terraform destroy` output followed by an empty `terraform state list`.

## Deliberate scope limits

An ALB or multiple writable application instances are not enabled yet. SecureShare currently stores encrypted files on a local Docker volume and uses process-local rate limiting. Horizontal scaling requires shared object storage, a shared database, and distributed rate-limit coordination. Kubernetes manifests can be demonstrated locally after that architecture change rather than adding a second deployment system prematurely.
