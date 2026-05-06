pipeline {
    agent any

    environment {
        // These match the exact IDs you just created in the Jenkins vault
        IMAGE_NAME = 'Hari2haran2/secureshare-api' 
        DOCKER_CRED_ID = 'hari2haran2'
        AWS_CRED_ID = 'aws-ssh-key-id'
        AWS_IP = '16.171.111.162'
    }

    stages {
        stage('Build Image') {
            steps {
                echo 'Compiling .NET code and building Docker Image...'
                // 'bat' tells Jenkins to run a Windows command prompt instruction
                bat "docker build -t ${IMAGE_NAME}:latest ."
            }
        }
        
        stage('Push to Registry') {
            steps {
                echo 'Securely logging into Docker Hub and pushing image...'
                // withCredentials temporarily injects your vault secrets into the environment variables
                withCredentials([usernamePassword(credentialsId: DOCKER_CRED_ID, passwordVariable: 'DOCKER_PASS', usernameVariable: 'DOCKER_USER')]) {
                    bat "echo %DOCKER_PASS% | docker login -u %DOCKER_USER% --password-stdin"
                    bat "docker push ${IMAGE_NAME}:latest"
                }
            }
        }

        stage('Deploy to AWS') {
            steps {
                echo 'Connecting to AWS server to deploy new container...'
                withCredentials([sshUserPrivateKey(credentialsId: AWS_CRED_ID, keyFileVariable: 'SSH_KEY', usernameVariable: 'SSH_USER')]) {
                    // 1. Connect via SSH without prompting for host key verification
                    // 2. Pull the new image, stop/remove the old container, and start the new one
                    bat "ssh -o StrictHostKeyChecking=no -i \"%SSH_KEY%\" %SSH_USER%@${AWS_IP} \"sudo docker pull ${IMAGE_NAME}:latest && sudo docker stop secureshare || true && sudo docker rm secureshare || true && sudo docker run -d -p 8080:8080 --name secureshare ${IMAGE_NAME}:latest\""
                }
            }
        }
    }
}