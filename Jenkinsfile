pipeline {
    agent {
        label 'build-agent'
    }

    options {
        skipDefaultCheckout(true)
    }

    environment {
        CONFIGURATION = 'Release'
        REPOSITORY_URL = 'https://github.com/apodaca21/MundialNube.git'
        REPOSITORY_BRANCH = 'ci/jenkins-activity'
    }

    stages {
        stage('Checkout') {
            steps {
                checkout([
                    $class: 'GitSCM',
                    branches: [[name: "*/${env.REPOSITORY_BRANCH}"]],
                    extensions: [],
                    userRemoteConfigs: [[
                        url: env.REPOSITORY_URL
                    ]]
                ])

                sh 'git log -1 --oneline'
            }
        }

        stage('Build') {
            steps {
                sh '''
                    dotnet restore TournamentServices.sln
                    dotnet build TournamentServices.sln \
                        --configuration "$CONFIGURATION" \
                        --no-restore
                '''
            }
        }

        stage('Unit Test') {
            steps {
               sh '''
    		rm -rf TestResults
    		mkdir -p TestResults

    		for project in tests/*/*.csproj; do
        		name=$(basename "$project" .csproj)

        		dotnet test "$project" \
            			--configuration "$CONFIGURATION" \
            			--no-build \
            			--logger "trx;LogFileName=${name}.trx" \
            			--results-directory TestResults
    			done
		''' 
            }

            post {
                always {
                    archiveArtifacts artifacts: 'TestResults/**/*',
                                     allowEmptyArchive: true
                }
            }
        }
    }
}
