# AWS resource lifecycle

AWS resources for this project must be declared in `teraform/main.tf` if they are expected to be removed by the normal destruction workflow. Do not create parallel EC2 instances, buckets, Lambda functions, alarms, load balancers, or networking resources manually.

## Optional course extensions

The backup bucket and scheduled health checker are disabled by default. Enable either in an ignored `.tfvars` file:

```hcl
enable_backup_bucket       = true
backup_retention_days      = 30
enable_health_check_lambda = true
health_check_url           = "https://example.invalid/health/ready"
```

The backup bucket blocks public access, enables versioning and encryption, and grants the EC2 role only list/get/put/multipart-abort access. It intentionally uses `force_destroy = true` so a course environment can be completely removed. Copy any backup that must survive before destruction.

The Lambda runs every five minutes, writes to a Terraform-managed CloudWatch log group retained for seven days, and has an error alarm. Terraform also owns its EventBridge rule, invocation permission and IAM role.

## Destruction audit

Run the repository script, then confirm that the state is empty:

```powershell
./scripts/Destroy-AwsEnvironment.ps1
terraform -chdir=teraform state list
```

An empty state confirms that Terraform removed everything it tracked. It does not cover resources created manually or through the separate CloudFormation template. Check AWS Resource Explorer and Cost Explorer for resources outside this state.

CloudFormation exercises must be removed by deleting their CloudFormation stack. Jenkins, Docker, Grafana, Prometheus, Node Exporter and ngrok are local services and are outside `terraform destroy`.

`Start-EphemeralAwsEnvironment.ps1` writes the Terraform instance addresses to `C:\ProgramData\Jenkins\.jenkins\secureshare-deployment.env`. This lets GitHub webhook builds deploy without entering the four changing IP addresses by hand. The destroy script removes the file only after Terraform reports an empty state.
