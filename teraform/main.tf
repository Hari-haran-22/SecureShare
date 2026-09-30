terraform {
  required_version = ">= 1.10, < 2.0"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
  }
  # Configure a private, encrypted remote state backend before team use.
}

provider "aws" {
  region              = var.region
  profile             = var.aws_profile
  allowed_account_ids = [var.expected_account_id]

  default_tags {
    tags = {
      Project     = "SecureShare"
      ManagedBy   = "Terraform"
      Environment = var.environment
    }
  }
}

variable "aws_profile" {
  description = "Named local AWS CLI profile. Leave null to use the environment credential chain."
  type        = string
  default     = null
  validation {
    condition     = var.aws_profile == null ? true : length(trimspace(var.aws_profile)) > 0
    error_message = "AWS profile must be null or a non-empty profile name."
  }
}

variable "expected_account_id" {
  description = "The AWS account allowed to provision SecureShare infrastructure."
  type        = string
  validation {
    condition     = can(regex("^[0-9]{12}$", var.expected_account_id))
    error_message = "Provide the new AWS account's 12-digit account ID."
  }
}

data "aws_caller_identity" "current" {}
data "aws_availability_zones" "available" { state = "available" }

output "aws_account_id" {
  value = data.aws_caller_identity.current.account_id
}

variable "region" {
  type    = string
  default = "us-east-1"
}
variable "environment" {
  description = "Deployment environment used for resource tagging."
  type        = string
  default     = "production"
}
variable "vpc_cidr" {
  description = "CIDR range for the dedicated SecureShare VPC."
  type        = string
  default     = "10.42.0.0/16"
  validation {
    condition     = can(cidrnetmask(var.vpc_cidr))
    error_message = "vpc_cidr must be a valid IPv4 CIDR."
  }
}
variable "budget_notification_email" {
  description = "Email address for AWS Budget alerts. Leave null to skip budget creation."
  type        = string
  default     = null
  nullable    = true
  validation {
    condition     = var.budget_notification_email == null ? true : can(regex("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", var.budget_notification_email))
    error_message = "Provide a valid budget notification email address or null."
  }
}
variable "monthly_budget_usd" {
  description = "Monthly cost budget in USD when budget notifications are enabled."
  type        = number
  default     = 5
  validation {
    condition     = var.monthly_budget_usd > 0
    error_message = "monthly_budget_usd must be greater than zero."
  }
}
variable "instance_type" {
  type    = string
  default = "t3.micro"
}
variable "ssh_key_name" {
  type    = string
  default = null
}
variable "ssh_cidrs" {
  type    = list(string)
  default = []
  validation {
    condition     = alltrue([for cidr in var.ssh_cidrs : can(cidrnetmask(cidr)) && cidr != "0.0.0.0/0"])
    error_message = "SSH requires explicit trusted IPv4 CIDRs; public SSH is forbidden."
  }
}

data "aws_ami" "ubuntu" {
  most_recent = true
  owners      = ["099720109477"] # Canonical

  filter {
    name   = "name"
    values = ["ubuntu/images/hvm-ssd-gp3/ubuntu-noble-24.04-amd64-server-*"]
  }

  filter {
    name   = "architecture"
    values = ["x86_64"]
  }

  filter {
    name   = "virtualization-type"
    values = ["hvm"]
  }
}

resource "aws_vpc" "secureshare" {
  cidr_block           = var.vpc_cidr
  enable_dns_support   = true
  enable_dns_hostnames = true
  tags                 = { Name = "SecureShare-VPC" }
}

resource "aws_internet_gateway" "secureshare" {
  vpc_id = aws_vpc.secureshare.id
  tags   = { Name = "SecureShare-Internet-Gateway" }
}

resource "aws_subnet" "public" {
  vpc_id                  = aws_vpc.secureshare.id
  cidr_block              = cidrsubnet(var.vpc_cidr, 8, 1)
  availability_zone       = data.aws_availability_zones.available.names[0]
  map_public_ip_on_launch = true
  tags                    = { Name = "SecureShare-Public" }
}

resource "aws_subnet" "private" {
  vpc_id            = aws_vpc.secureshare.id
  cidr_block        = cidrsubnet(var.vpc_cidr, 8, 2)
  availability_zone = data.aws_availability_zones.available.names[0]
  tags              = { Name = "SecureShare-Private" }
}

resource "aws_route_table" "public" {
  vpc_id = aws_vpc.secureshare.id
  route {
    cidr_block = "0.0.0.0/0"
    gateway_id = aws_internet_gateway.secureshare.id
  }
  tags = { Name = "SecureShare-Public-Routes" }
}

resource "aws_route_table_association" "public" {
  subnet_id      = aws_subnet.public.id
  route_table_id = aws_route_table.public.id
}

resource "aws_route_table" "private" {
  vpc_id = aws_vpc.secureshare.id
  tags   = { Name = "SecureShare-Private-Routes" }
}

resource "aws_route_table_association" "private" {
  subnet_id      = aws_subnet.private.id
  route_table_id = aws_route_table.private.id
}

resource "aws_security_group" "secureshare_sg" {
  name_prefix = "secureshare-"
  description = "Public HTTPS with optional restricted SSH; monitoring stays private"
  vpc_id      = aws_vpc.secureshare.id

  ingress {
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }
  ingress {
    from_port   = 443
    to_port     = 443
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }
  dynamic "ingress" {
    for_each = length(var.ssh_cidrs) > 0 ? [1] : []
    content {
      from_port   = 22
      to_port     = 22
      protocol    = "tcp"
      cidr_blocks = var.ssh_cidrs
    }
  }
  ingress {
    description = "API metrics from the private services node"
    from_port   = 8080
    to_port     = 8080
    protocol    = "tcp"
    cidr_blocks = [aws_vpc.secureshare.cidr_block]
  }
  # Public HTTP is required for Ubuntu package repositories and certificate redirects.
  egress {
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }
  # Public HTTPS is required for package updates, container pulls, SSM, and ACME.
  egress {
    from_port   = 443
    to_port     = 443
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }
  egress {
    description = "Malware scanning on the private services node"
    from_port   = 3310
    to_port     = 3310
    protocol    = "tcp"
    cidr_blocks = [aws_vpc.secureshare.cidr_block]
  }
  tags = { Name = "SecureShare-App-SG" }
}

resource "aws_security_group" "services_sg" {
  name_prefix = "secureshare-services-"
  description = "Private scanner and monitoring services"
  vpc_id      = aws_vpc.secureshare.id

  dynamic "ingress" {
    for_each = length(var.ssh_cidrs) > 0 ? [1] : []
    content {
      from_port   = 22
      to_port     = 22
      protocol    = "tcp"
      cidr_blocks = var.ssh_cidrs
    }
  }
  ingress {
    description = "ClamAV requests from the application node"
    from_port   = 3310
    to_port     = 3310
    protocol    = "tcp"
    cidr_blocks = [aws_vpc.secureshare.cidr_block]
  }
  # Public HTTPS is needed for package updates, container pulls, and SSM.
  egress {
    from_port   = 443
    to_port     = 443
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }
  # Ubuntu package repositories can redirect to public HTTP mirrors.
  egress {
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }
  egress {
    description = "Prometheus scrapes the application over the VPC"
    from_port   = 8080
    to_port     = 8080
    protocol    = "tcp"
    cidr_blocks = [aws_vpc.secureshare.cidr_block]
  }
  tags = { Name = "SecureShare-Services-SG" }
}

resource "aws_iam_role" "instance" {
  name_prefix = "secureshare-ssm-"
  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Action    = "sts:AssumeRole"
      Effect    = "Allow"
      Principal = { Service = "ec2.amazonaws.com" }
    }]
  })
}
resource "aws_iam_role_policy_attachment" "ssm" {
  role       = aws_iam_role.instance.name
  policy_arn = "arn:aws:iam::aws:policy/AmazonSSMManagedInstanceCore"
}
resource "aws_iam_instance_profile" "instance" {
  name_prefix = "secureshare-"
  role        = aws_iam_role.instance.name
}

resource "aws_instance" "app_server" {
  ami                         = data.aws_ami.ubuntu.id
  instance_type               = var.instance_type
  key_name                    = var.ssh_key_name
  iam_instance_profile        = aws_iam_instance_profile.instance.name
  subnet_id                   = aws_subnet.public.id
  associate_public_ip_address = true
  vpc_security_group_ids      = [aws_security_group.secureshare_sg.id]

  metadata_options {
    http_tokens = "required"
  }
  root_block_device {
    volume_size           = 30
    volume_type           = "gp3"
    encrypted             = true
    delete_on_termination = true
  }
  user_data = <<-EOF
    #!/bin/bash
    set -euo pipefail
    apt-get update
    apt-get install -y ca-certificates curl git
    install -m 0755 -d /etc/apt/keyrings
    curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
    chmod a+r /etc/apt/keyrings/docker.asc
    echo "deb [arch=amd64 signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu noble stable" > /etc/apt/sources.list.d/docker.list
    apt-get update
    apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
    systemctl enable --now docker
    usermod -aG docker ubuntu
    fallocate -l 4G /swapfile
    chmod 600 /swapfile
    mkswap /swapfile
    swapon /swapfile
    echo '/swapfile none swap sw 0 0' >> /etc/fstab
    install -d -m 0700 /opt/secureshare
    chown ubuntu:ubuntu /opt/secureshare
  EOF
  tags = {
    Name    = "SecureShare-App"
    Project = "SecureShare"
    Role    = "app"
  }
}

moved {
  from = aws_instance.secureshare_server
  to   = aws_instance.app_server
}

resource "aws_instance" "services_server" {
  ami                         = data.aws_ami.ubuntu.id
  instance_type               = var.instance_type
  key_name                    = var.ssh_key_name
  iam_instance_profile        = aws_iam_instance_profile.instance.name
  subnet_id                   = aws_subnet.public.id
  associate_public_ip_address = true
  vpc_security_group_ids      = [aws_security_group.services_sg.id]

  metadata_options {
    http_tokens = "required"
  }
  root_block_device {
    volume_size           = 16
    volume_type           = "gp3"
    encrypted             = true
    delete_on_termination = true
  }
  user_data = <<-EOF
    #!/bin/bash
    set -euo pipefail
    apt-get update
    apt-get install -y ca-certificates curl git
    install -m 0755 -d /etc/apt/keyrings
    curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
    chmod a+r /etc/apt/keyrings/docker.asc
    echo "deb [arch=amd64 signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu noble stable" > /etc/apt/sources.list.d/docker.list
    apt-get update
    apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
    systemctl enable --now docker
    usermod -aG docker ubuntu
    fallocate -l 4G /swapfile
    chmod 600 /swapfile
    mkswap /swapfile
    swapon /swapfile
    echo '/swapfile none swap sw 0 0' >> /etc/fstab
    install -d -m 0700 /opt/secureshare
    chown ubuntu:ubuntu /opt/secureshare
  EOF
  tags = {
    Name    = "SecureShare-Services"
    Project = "SecureShare"
    Role    = "services"
  }
}

resource "aws_budgets_budget" "monthly" {
  count        = var.budget_notification_email == null ? 0 : 1
  name         = "SecureShare-Monthly-Cost"
  budget_type  = "COST"
  limit_amount = tostring(var.monthly_budget_usd)
  limit_unit   = "USD"
  time_unit    = "MONTHLY"

  cost_filter {
    name   = "TagKeyValue"
    values = ["user:Project$SecureShare"]
  }

  notification {
    comparison_operator        = "GREATER_THAN"
    threshold                  = 80
    threshold_type             = "PERCENTAGE"
    notification_type          = "FORECASTED"
    subscriber_email_addresses = [var.budget_notification_email]
  }

  notification {
    comparison_operator        = "GREATER_THAN"
    threshold                  = 100
    threshold_type             = "PERCENTAGE"
    notification_type          = "ACTUAL"
    subscriber_email_addresses = [var.budget_notification_email]
  }
}
output "server_public_ip" {
  value = aws_instance.app_server.public_ip
}
output "instance_id" {
  value = aws_instance.app_server.id
}
output "services_public_ip" {
  value = aws_instance.services_server.public_ip
}
output "services_private_ip" {
  value = aws_instance.services_server.private_ip
}
output "services_instance_id" {
  value = aws_instance.services_server.id
}
output "vpc_id" {
  value = aws_vpc.secureshare.id
}
output "private_subnet_id" {
  description = "Reserved private subnet for future data services; it intentionally has no internet route."
  value       = aws_subnet.private.id
}
