# 1. Tell Terraform we are using AWS
provider "aws" {
  region = "us-east-1"
}

# 2. Create the Security Group (Firewall Rules)
resource "aws_security_group" "secureshare_sg" {
  name        = "secureshare_web_traffic"
  description = "Allow port 8080 and SSH"

  ingress {
    from_port   = 8080
    to_port     = 8080
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"] 
  }

  ingress {
    from_port   = 22
    to_port     = 22
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"] 
  }
  ingress{
    from_port   = 3000
    to_port     = 3000
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
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
  ami           = "ami-0c7217cdde317cfec" 
  instance_type = "t3.micro"              
  key_name      = "terraform-deploy-key"     

  vpc_security_group_ids = [aws_security_group.secureshare_sg.id]

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

# 4. Output the public IP
output "server_public_ip" {
  value = aws_instance.secureshare_server.public_ip
}