# Synopsis implementation

The source synopsis is stored at [SecureShare_Synopsis.docx](SecureShare_Synopsis.docx). It describes the intended DevSecOps and cloud architecture. The repository now implements every proposed enhancement, with the following project-specific choices.

| Synopsis commitment | Repository implementation |
| --- | --- |
| Externalize SQL credentials | `scripts/Initialize.ps1` and `scripts/initialize.sh` generate ignored local secrets. Compose requires `MSSQL_SA_PASSWORD` and `GRAFANA_ADMIN_PASSWORD`; no password is committed. Production uses SQLite and a certificate password file mounted read-only. |
| Scan containers before deployment | `Jenkinsfile` runs Trivy against the source tree and the built image. High or critical findings stop the pipeline before deployment. |
| Use a dedicated VPC | `teraform/main.tf` creates a VPC, internet gateway, public and private subnets, and separate route tables. The current two EC2 nodes use the public subnet because Jenkins deploys over restricted SSH and both nodes need package and image downloads. The isolated private subnet is reserved for a future database or internal service; adding a NAT gateway by default would add hourly cost. |
| Provide CloudFormation for comparison | `/infrastructure.yaml` is the CloudFormation equivalent of the two-node Terraform stack, including networking, security groups, IAM, tagging, and optional budget alerts. Terraform remains the deployment source of truth. |
| Use least privilege instance roles | Both EC2 instances receive an instance profile containing only `AmazonSSMManagedInstanceCore`. Application deployment uses the Jenkins SSH credential; no AWS access key is stored in the repository or on the instances. |
| Add tags and cost alerts | The Terraform provider applies `Project`, `ManagedBy`, and `Environment` tags to supported resources. Both IaC templates can create an AWS Budget with forecasted 80% and actual 100% email alerts when a notification address is supplied. |

The synopsis calls the baseline “.NET 8.” The application has since moved to .NET 10, as recorded by the project files, Dockerfile, Jenkins build, and README. The synopsis is retained unchanged as the supplied academic/project source document; this implementation record describes the current repository.

## Terraform budget configuration

Budget creation is optional because AWS requires a real subscriber address. Add these values to an ignored `.tfvars` file:

```hcl
budget_notification_email = "owner@example.com"
monthly_budget_usd        = 5
```

AWS sends a subscription/alert email to the configured address. Terraform does not destroy compute automatically when the threshold is reached; the existing Jenkins destroy job remains the explicit lifecycle control for ephemeral environments.

## CloudFormation comparison

Validate the template against the authenticated AWS account before creating a stack:

```powershell
aws cloudformation validate-template --template-body file://infrastructure.yaml
```

Provide the region-specific Ubuntu 24.04 AMI through `AmiId`. Supplying `BudgetNotificationEmail` creates the cost budget. Supplying `SshKeyName` and a restricted `SshCidr` enables the same Jenkins SSH deployment path used by Terraform.
