# 1. Tell Terraform we are using AWS
provider "aws" {
  region = "us-east-1" # Change to your preferred region
}

# 2. Create the Security Group (Firewall Rules)
resource "aws_security_group" "secureshare_sg" {
  name        = "secureshare_web_traffic"
  description = "Allow port 8080 and SSH"

  ingress {
    description = "Allow Web Traffic"
    from_port   = 8080
    to_port     = 8080
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"] # Open to the internet
  }

  ingress {
    description = "Allow SSH for Jenkins"
    from_port   = 22
    to_port     = 22
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"] # In production, restrict this to Jenkins' IP
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

# 3. Create the EC2 Instance
resource "aws_instance" "secureshare_server" {
  ami           = "ami-0c7217cdde317cfec" # Standard Ubuntu 22.04 AMI
  instance_type = "t2.micro"              # AWS Free Tier
  key_name      = "your-aws-key-name"     # Your existing SSH key pair name

  vpc_security_group_ids = [aws_security_group.secureshare_sg.id]

  # Automatically install Docker when the server first boots!
  user_data = <<-EOF
              #!/bin/bash
              sudo apt-get update -y
              sudo apt-get install docker.io -y
              sudo systemctl start docker
              sudo systemctl enable docker
              sudo usermod -aG docker ubuntu
              EOF

  tags = {
    Name = "SecureShare-Production"
  }
}

# 4. Output the public IP so Jenkins knows where to deploy
output "server_public_ip" {
  value = aws_instance.secureshare_server.public_ip
}