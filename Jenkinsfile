pipeline {
    agent { label 'linux-docker-dotnet' }
    options {
        disableConcurrentBuilds()
        timestamps()
        timeout(time: 30, unit: 'MINUTES')
    }
    triggers { githubPush() }
    parameters {
        string(name: 'IMAGE_NAME', defaultValue: 'hari2haran2/secureshare-api', description: 'Docker Hub image repository')
        string(name: 'DEPLOY_HOST', defaultValue: '13.60.23.3', description: 'Approved AWS deployment host')
        string(name: 'SSH_CREDENTIAL_ID', defaultValue: 'aws-ssh-key-id', description: 'Jenkins SSH key credential for the selected AWS server')
    }
    stages {
        stage('Validate inputs') {
            steps {
                script {
                    if (!(params.IMAGE_NAME ==~ /[a-z0-9][a-z0-9._\/-]+/) ||
                        !(params.DEPLOY_HOST ==~ /[a-zA-Z0-9.-]*/) ||
                        !(params.SSH_CREDENTIAL_ID ==~ /[a-zA-Z0-9_.-]+/)) {
                        error('Invalid image or deployment address')
                    }
                }
            }
        }
        stage('Restore, build and test') {
            steps {
                sh 'dotnet restore SecureShare.slnx --locked-mode'
                sh 'dotnet test SecureShare.slnx -c Release --no-restore --logger "trx;LogFileName=tests.trx"'
                sh 'dotnet ef migrations has-pending-model-changes --project SecureShare.API --configuration Release --no-build'
            }
            post { always { archiveArtifacts artifacts: '**/TestResults/*.trx', allowEmptyArchive: true } }
        }
        stage('Security and infrastructure checks') {
            steps {
                sh 'trivy fs --scanners vuln,secret,misconfig --exit-code 1 --severity HIGH,CRITICAL --skip-dirs .git,SecureShare.API/SecureUploads,secrets,backups .'
                sh 'terraform -chdir=teraform fmt -check'
                sh 'terraform -chdir=teraform init -backend=false'
                sh 'terraform -chdir=teraform validate'
            }
        }
        stage('Build and scan image') {
            steps {
                sh 'docker build --pull -t "$IMAGE_NAME:$GIT_COMMIT" .'
                sh 'trivy image --exit-code 1 --severity HIGH,CRITICAL "$IMAGE_NAME:$GIT_COMMIT"'
            }
        }
        stage('Push image') {
            steps {
                withCredentials([usernamePassword(credentialsId: 'docker-hub-id', passwordVariable: 'DOCKER_PASS', usernameVariable: 'DOCKER_USER')]) {
                    sh '''
                        set +x
                        printf '%s' "$DOCKER_PASS" | docker login -u "$DOCKER_USER" --password-stdin
                        docker push "$IMAGE_NAME:$GIT_COMMIT"
                        docker logout
                    '''
                }
            }
        }
        stage('Deploy and verify') {
            when { expression { params.DEPLOY_HOST.trim() != '' } }
            steps {
                withCredentials([
                    sshUserPrivateKey(credentialsId: params.SSH_CREDENTIAL_ID,
                                      keyFileVariable: 'SSH_KEY',
                                      usernameVariable: 'SSH_USERNAME')
                ]) {
                        sh '''
                            set +x
                            ssh -i "$SSH_KEY" -o IdentitiesOnly=yes -o StrictHostKeyChecking=yes \
                                -o UserKnownHostsFile="$WORKSPACE/deploy/known_hosts" "$SSH_USERNAME@$DEPLOY_HOST" \
                                bash -s -- "$GIT_COMMIT" "$IMAGE_NAME:$GIT_COMMIT" < scripts/remote-deploy.sh
                        '''
                }
            }
        }
    }
}
