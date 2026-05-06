pipeline {
    agent any

    environment {
        // These match the exact IDs you just created in the Jenkins vault
        IMAGE_NAME = 'hari2haran2/secureshare-api' 
        DOCKER_CRED_ID = 'docker-hub-id'
        AWS_CRED_ID = 'aws-ssh-key-id'
        AWS_IP = '16.171.111.162'
        DOCKER_HOST = 'tcp://localhost:2375'
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
                withCredentials([usernamePassword(credentialsId: DOCKER_CRED_ID, passwordVariable: 'DOCKER_PASS', usernameVariable: 'DOCKER_USER')]) {
                    // Bypassing the Windows echo pipe by using the direct -p flag
                    bat "docker login -u %DOCKER_USER% -p \"%DOCKER_PASS%\""
                    bat "docker push ${IMAGE_NAME}:latest"
                }
            }
        }

        stage('Deploy to AWS') {
            steps {
                echo 'Connecting to AWS server to deploy new container...'
                withCredentials([sshUserPrivateKey(credentialsId: 'aws-ssh-key-id', keyFileVariable: 'SSH_KEY', usernameVariable: 'SSH_USER')]) {
                    
                    // 1. Get the EXACT active Windows background user and lock the key to them
                    bat "FOR /F \"tokens=*\" %%i IN ('whoami') DO icacls \"%SSH_KEY%\" /inheritance:r /grant \"%%i:R\""
                    
                    // 2. Connect via SSH and execute the Docker deployment commands
                    bat "ssh -o StrictHostKeyChecking=no -i \"%SSH_KEY%\" %SSH_USER%@16.171.111.162 \"sudo docker pull hari2haran2/secureshare-api:latest && sudo docker stop secureshare || true && sudo docker rm secureshare || true && sudo docker run -d -p 8080:8080 --name secureshare hari2haran2/secureshare-api:latest\""
                }
            }
        }
    }
}
