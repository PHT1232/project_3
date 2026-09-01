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
//
// Deploy stage also runs Elasticsearch + Kibana containers (ports 9200/5601) so the backend
// can ship logs via Serilog (Elasticsearch__Uri env var, see Program.cs). Elasticsearch data
// persists across builds in the named volume stationeryms-es-data, since the container is
// recreated (docker rm -f) on every deploy.
pipeline {
    agent any

    environment {
        BACKEND_IMAGE  = 'stationeryms-backend'
        FRONTEND_IMAGE = 'stationeryms-frontend'
        IMAGE_TAG      = "${env.BUILD_ID}"
        DOCKER_NETWORK = 'stationeryms-net'
        BACKEND_CONTAINER  = 'stationeryms-backend'
        FRONTEND_CONTAINER = 'stationeryms-frontend'
        ELASTICSEARCH_CONTAINER = 'stationeryms-elasticsearch'
        KIBANA_CONTAINER   = 'stationeryms-kibana'
        ES_VOLUME          = 'stationeryms-es-data'
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
                sh "docker volume create ${ES_VOLUME} || true"

                echo 'Deploying Elasticsearch container...'
                sh "docker rm -f ${ELASTICSEARCH_CONTAINER} || true"
                sh """
                    docker run -d \
                        --name ${ELASTICSEARCH_CONTAINER} \
                        --network ${DOCKER_NETWORK} \
                        -p 9200:9200 \
                        -e discovery.type=single-node \
                        -e xpack.security.enabled=false \
                        -e ES_JAVA_OPTS="-Xms512m -Xmx512m" \
                        -v ${ES_VOLUME}:/usr/share/elasticsearch/data \
                        --restart unless-stopped \
                        docker.elastic.co/elasticsearch/elasticsearch:8.15.0
                """

                echo 'Deploying Kibana container...'
                sh "docker rm -f ${KIBANA_CONTAINER} || true"
                sh """
                    docker run -d \
                        --name ${KIBANA_CONTAINER} \
                        --network ${DOCKER_NETWORK} \
                        -p 5601:5601 \
                        -e ELASTICSEARCH_HOSTS=http://${ELASTICSEARCH_CONTAINER}:9200 \
                        --restart unless-stopped \
                        docker.elastic.co/kibana/kibana:8.15.0
                """

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
                        -e Elasticsearch__Uri="http://${ELASTICSEARCH_CONTAINER}:9200" \
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
            echo "Backend (port 8080, /swagger for API docs), frontend (port 8081), Elasticsearch (port 9200) and Kibana (port 5601) deployed as independent containers. ✅"
        }
        failure {
            echo 'Pipeline failed ❌ — check the stage logs above.'
        }
    }
}
