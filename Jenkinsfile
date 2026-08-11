pipeline {
    agent any

    environment {
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        BUILD_CONFIGURATION = 'Release'
    }

    stages {
        stage('Checkout') {
            steps {
                checkout scm
            }
        }

        stage('Restore Dependencies') {
            steps {
                bat 'dotnet restore Neas.SalesManager.slnx'
            }
        }

        stage('Build Solution') {
            steps {
                bat "dotnet build Neas.SalesManager.slnx --configuration ${BUILD_CONFIGURATION} --no-restore"
            }
        }

        stage('Run Automated Tests') {
            steps {
                bat "dotnet test Neas.SalesManager.slnx --configuration ${BUILD_CONFIGURATION} --no-build --logger trx"
            }
        }

        stage('Publish Artifacts') {
            steps {
                bat "dotnet publish src/Neas.SalesManager.Api/Neas.SalesManager.Api.csproj -c ${BUILD_CONFIGURATION} -o ./publish/api"
                bat "dotnet publish src/Neas.SalesManager.Wpf/Neas.SalesManager.Wpf.csproj -c ${BUILD_CONFIGURATION} -o ./publish/wpf"
            }
        }
    }

    post {
        always {
            junit '**/TestResults/*.trx'
        }
    }
}