# Switch SecureShare to a new AWS account

The API uses local SQL Server and storage; it does not contain an AWS account credential. Jenkins deployment uses credentials stored in Jenkins. A local AWS CLI profile is needed only when running Terraform on this computer.

## Switch Jenkins credentials

The `aws-ssh-key-id` credential is an **SSH Username with private key** credential used to connect to EC2. Replace its private key and username in Jenkins when the server key pair changes, or create a new credential and set `SSH_CREDENTIAL_ID` to that ID. Set `DEPLOY_HOST` to the new server address and replace `deploy/known_hosts` with its independently verified SSH host key. The pipeline reads the SSH username from the credential.

The Jenkins instance was inspected on 2026-09-19. Its `aws-ssh-key-id` entry is an **SSH Username with private key** credential for `ubuntu`, and it is used by `SecureShare-Deployment`. No AWS access-key credential is stored there. The repository references only the credential ID; it never contains the private key.

An SSH private key is tied to a key pair on an EC2 server, not directly to an AWS account API. Replace the concealed private key directly in Jenkins under **Manage Jenkins → Credentials** if the new server uses a different key, and configure the new host parameters. Terraform still needs separately authenticated AWS credentials to create or inspect infrastructure in account `593602867169`.

## Configure and select the replacement account

1. Configure a named AWS CLI profile on this computer. With AWS CLI 2.32.0 or newer, browser sign-in is available through `aws login --profile secureshare-new --region us-east-1`. Sign in to the replacement account and approve the CLI connection in the browser. For IAM Identity Center, run `aws configure sso --profile secureshare-new`, then `aws sso login --profile secureshare-new`. For access-key authentication, run `aws configure --profile secureshare-new` and enter credentials in your local terminal. Do not commit credentials or share them in chat.
2. Verify the account ID with `aws sts get-caller-identity --profile secureshare-new --query Account --output text`.
3. Select it for this project:

```powershell
./scripts/Use-AwsAccount.ps1 -Profile secureshare-new -AccountId YOUR_12_DIGIT_ACCOUNT_ID -Region us-east-1
```

The script verifies the profile's actual account ID before writing the ignored `teraform/account.auto.tfvars.json`. Terraform's `allowed_account_ids` guard rejects credentials for another account. This does not change the global `default` AWS profile.

For this project, the requested replacement account is `593602867169`. The current ignored `account.auto.tfvars.json` leaves `aws_profile` null so Terraform reads credentials from Jenkins or the standard AWS environment credential chain. Terraform's `allowed_account_ids` check prevents credentials from another account from creating resources.

If you later configure a working local `hariharan` profile, verify and select it with:

```powershell
./scripts/Use-AwsAccount.ps1 -Profile hariharan -AccountId 593602867169 -Region us-east-1
```

Browser sign-in uses temporary credentials. If an older Terraform AWS SDK cannot read the login profile, use AWS's documented `credential_process` bridge with a separate login profile; do not copy temporary keys into the project. See [AWS CLI browser sign-in and SDK compatibility](https://docs.aws.amazon.com/cli/latest/userguide/cli-configure-sign-in.html).

If you use environment or assumed-role credentials instead of a local profile, configure `expected_account_id` in your own ignored variable file and leave `aws_profile` null. Make sure the account selection file does not override your intended credential settings.

## Terraform state and provisioning

The previous local state was inspected on 2026-09-17 and contained zero resources. Preserve it and its backup. If state later contains resources from a different account, use separately configured state for the replacement account; do not reuse or delete the original account's state to force a deployment.

From the `teraform` directory:

```powershell
terraform init
terraform plan -out=new-account.tfplan
# Review the plan before applying. Applying creates billable cloud resources.
terraform apply new-account.tfplan
terraform output aws_account_id
terraform output server_public_ip
```

Terraform uses the region you selected and obtains the Ubuntu AMI for that region. SSH key pairs are account- and region-specific: create a replacement key pair and configure `ssh_key_name` and restricted `ssh_cidrs` if using SSH. Session Manager is configured by default.

The default server is `t3.micro` with a 30 GB encrypted gp3 root disk, both marked Free Tier eligible by AWS. No Elastic IP is allocated. Eligibility and credits depend on the account and expire according to AWS's Free Tier terms; verify the Cost and Usage widget before applying. A `t3.micro` has only 1 GiB of memory, so the full SQL Server, ClamAV, Prometheus, and Grafana production stack may need a larger instance after initial testing.

## Connect Jenkins to the new server

1. Bootstrap the new server using the production setup in README.md, including `/opt/secureshare`, server secrets, the wrapping certificate, and DNS.
2. Store the new server's private SSH key in Jenkins and pin its independently verified public host key in `deploy/known_hosts`.
3. Set the pipeline parameters `DEPLOY_HOST` and `SSH_CREDENTIAL_ID` to the replacement server's values. The SSH username comes from `SSH_CREDENTIAL_ID`.
4. Update your domain's DNS and any off-server backup destination. Restore a retained file/database/key archive if you need old uploads; changing accounts does not transfer EC2 disks or database data automatically.

Docker Hub credentials are separate from AWS and can continue to be used if that registry account is available. Terraform account selection does not automatically change Jenkins credentials.
