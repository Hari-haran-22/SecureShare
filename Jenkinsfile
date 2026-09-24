pipeline {
    agent any
    options {
        skipDefaultCheckout(true)
        disableConcurrentBuilds()
        timestamps()
        timeout(time: 30, unit: 'MINUTES')
    }
    triggers { githubPush() }
    environment {
        DOCKER_HOST = 'tcp://127.0.0.1:2375'
    }
    parameters {
        string(name: 'IMAGE_NAME', defaultValue: 'hari2haran2/secureshare-api', description: 'Docker Hub image repository')
        string(name: 'DEPLOY_HOST', defaultValue: '13.60.23.3', description: 'Approved AWS deployment host')
        string(name: 'SSH_CREDENTIAL_ID', defaultValue: 'aws-ssh-key-id', description: 'Jenkins SSH key credential for the selected AWS server')
    }
    stages {
        stage('Checkout') {
            steps {
                deleteDir()
                bat 'git clone --depth 1 --branch master https://github.com/Hari-haran-22/SecureShare.git .'
                script {
                    env.GIT_COMMIT = bat(script: '@git rev-parse HEAD', returnStdout: true).trim()
                }
            }
        }
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
                bat 'dotnet restore SecureShare.slnx --locked-mode'
                bat 'dotnet test SecureShare.slnx -c Release --no-restore --logger "trx;LogFileName=tests.trx"'
                bat '"C:\\Users\\surya\\.dotnet\\tools\\dotnet-ef.exe" migrations has-pending-model-changes --project SecureShare.API --configuration Release --no-build'
            }
            post { always { archiveArtifacts artifacts: '**/TestResults/*.trx', allowEmptyArchive: true } }
        }
        stage('Security and infrastructure checks') {
            steps {
                bat 'docker run --rm -v "%WORKSPACE%:/src" -w /src aquasec/trivy:0.67.2 fs --scanners vuln,secret,misconfig --exit-code 1 --severity HIGH,CRITICAL --skip-dirs .git,SecureShare.API/SecureUploads,secrets,backups .'
                bat '"C:\\Users\\surya\\AppData\\Local\\Microsoft\\WinGet\\Links\\terraform.exe" -chdir=teraform fmt -check'
                bat '"C:\\Users\\surya\\AppData\\Local\\Microsoft\\WinGet\\Links\\terraform.exe" -chdir=teraform init -backend=false'
                bat '"C:\\Users\\surya\\AppData\\Local\\Microsoft\\WinGet\\Links\\terraform.exe" -chdir=teraform validate'
            }
        }
        stage('Build and scan image') {
            steps {
                bat 'docker build --pull -t "%IMAGE_NAME%:%GIT_COMMIT%" .'
                bat 'docker run --rm -v /var/run/docker.sock:/var/run/docker.sock aquasec/trivy:0.67.2 image --exit-code 1 --severity HIGH,CRITICAL "%IMAGE_NAME%:%GIT_COMMIT%"'
            }
        }
        stage('Push image') {
            steps {
                withCredentials([usernamePassword(credentialsId: 'docker-hub-id', passwordVariable: 'DOCKER_PASS', usernameVariable: 'DOCKER_USER')]) {
                    bat '''
                        @echo off
                        echo %DOCKER_PASS%| docker login -u "%DOCKER_USER%" --password-stdin
                        docker push "%IMAGE_NAME%:%GIT_COMMIT%"
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
                        bat '''
                            @echo off
                            for /f "tokens=*" %%i in ('whoami') do icacls "%SSH_KEY%" /inheritance:r /grant "%%i:R"
                            ssh -i "%SSH_KEY%" -o IdentitiesOnly=yes -o StrictHostKeyChecking=yes ^
                                -o UserKnownHostsFile="%WORKSPACE%\\deploy\\known_hosts" "%SSH_USERNAME%@%DEPLOY_HOST%" ^
                                bash -s -- "%GIT_COMMIT%" "%IMAGE_NAME%:%GIT_COMMIT%" < scripts\\remote-deploy.sh
                        '''
                }
            }
        }
    }
}
