pipeline {
    agent {
        label 'build-agent'
    }

    stages {
        stage('Build Environment') {
            steps {
                sh '''
                    dotnet --info
                    podman version
                '''
            }
        }

        stage('Deployment Environment') {
            agent {
                label 'deploy-agent'
            }

            steps {
                sh '''
                    git --version
                    kubectl version --client
                    curl --version
                '''
            }
        }
    }
}
