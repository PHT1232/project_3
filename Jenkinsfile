// Builds and deploys the backend (WebApi/Dockerfile) and frontend (frontend/Dockerfile) as
// two independent containers on a shared Docker network, rather than the single combined
// image the root Dockerfile produces.
//
// Required Jenkins credentials (Manage Jenkins > Credentials), all "Secret text":
//   stationeryms-db-connection-string     — SQL Server connection string
//   stationeryms-jwt-signing-key          — HS256 signing key, 32+ bytes
//   stationeryms-bootstrap-admin-password — initial password for the seeded MD account
//     (employee #1); Program.cs throws and the container exits at startup if this is
//     missing in any non-Testing environment, which looks like a 502 from the outside,
//     not an auth error — see AI_usage_report.md 2026-08-28 "bootstrap admin".
pipeline {
    agent any

    environment {
        BACKEND_IMAGE  = 'stationeryms-backend'
        FRONTEND_IMAGE = 'stationeryms-frontend'
        IMAGE_TAG      = "${env.BUILD_ID}"
        DOCKER_NETWORK = 'stationeryms-net'
        BACKEND_CONTAINER  = 'stationeryms-backend'
        FRONTEND_CONTAINER = 'stationeryms-frontend'
        JWT_SIGNING_KEY           = credentials('stationeryms-jwt-signing-key')
        DB_CONNECTION_STRING      = credentials('stationeryms-db-connection-string')
        BOOTSTRAP_ADMIN_PASSWORD  = credentials('stationeryms-bootstrap-admin-password')
    }

    stages {
        stage('Checkout') {
            steps {
                checkout scm
            }
        }

        stage('Backend: restore, build, test') {
            steps {
                sh 'dotnet restore Project.slnx'
                sh 'dotnet build Project.slnx --no-restore -c Release'
                sh 'dotnet test Project.slnx --no-restore -c Release --logger "console;verbosity=normal"'
            }
        }

        stage('Frontend: install, build, test') {
            steps {
                dir('frontend') {
                    sh 'npm ci'
                    sh 'npm run test'
                    sh 'npm run build'
                }
            }
        }

        stage('Build images') {
            parallel {
                stage('Backend image') {
                    steps {
                        sh "docker build -f WebApi/Dockerfile -t ${BACKEND_IMAGE}:${IMAGE_TAG} -t ${BACKEND_IMAGE}:latest ."
                    }
                }
                stage('Frontend image') {
                    steps {
                        sh "docker build -t ${FRONTEND_IMAGE}:${IMAGE_TAG} -t ${FRONTEND_IMAGE}:latest ./frontend"
                    }
                }
            }
        }

        stage('Deploy') {
            steps {
                sh "docker network create ${DOCKER_NETWORK} || true"

                echo 'Deploying backend container...'
                sh "docker rm -f ${BACKEND_CONTAINER} || true"
                sh """
                    docker run -d \
                        --name ${BACKEND_CONTAINER} \
                        --network ${DOCKER_NETWORK} \
                        -p 8080:8080 \
                        -e ASPNETCORE_ENVIRONMENT=Production \
                        -e ConnectionStrings__DefaultConnection="${DB_CONNECTION_STRING}" \
                        -e Jwt__SigningKey="${JWT_SIGNING_KEY}" \
                        -e Seed__BootstrapAdminPassword="${BOOTSTRAP_ADMIN_PASSWORD}" \
                        --restart unless-stopped \
                        ${BACKEND_IMAGE}:${IMAGE_TAG}
                """

                echo 'Deploying frontend container...'
                sh "docker rm -f ${FRONTEND_CONTAINER} || true"
                sh """
                    docker run -d \
                        --name ${FRONTEND_CONTAINER} \
                        --network ${DOCKER_NETWORK} \
                        -p 8081:80 \
                        -e BACKEND_HOST=${BACKEND_CONTAINER} \
                        -e BACKEND_PORT=8080 \
                        --restart unless-stopped \
                        ${FRONTEND_IMAGE}:${IMAGE_TAG}
                """
            }
        }
    }

    post {
        always {
            sh 'docker image prune -f || true'
        }
        success {
            echo "Backend (port 8080, /swagger for API docs) and frontend (port 8081) deployed as independent containers. ✅"
        }
        failure {
            echo 'Pipeline failed ❌ — check the stage logs above.'
        }
    }
}
