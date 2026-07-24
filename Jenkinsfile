pipeline {
    // Pipeline này sử dụng docker client trên máy chủ Jenkins để build container
    agent any

    environment {
        // Tên image tuỳ chỉnh
        IMAGE_NAME = 'my-fullstack-app'
        // Có thể gắn tag theo build number của Jenkins
        IMAGE_TAG = "${env.BUILD_ID}"
    }

    stages {
        stage('Checkout') {
            steps {
                echo 'Checking out source code...'
                checkout scm
            }
        }

        stage('Build Docker Image') {
            steps {
                echo "Building Docker image ${IMAGE_NAME}:${IMAGE_TAG}..."
                // Dockerfile ở thư mục gốc sẽ lo toàn bộ việc build backend & frontend
                sh 'docker build -t ${IMAGE_NAME}:${IMAGE_TAG} -t ${IMAGE_NAME}:latest .'
            }
        }

        stage('Deploy / Run') {
            steps {
                echo 'Bật container từ image vừa build...'
                // Dừng và xoá container cũ (nếu có)
                sh 'docker rm -f my-running-app || true'
                
                // Chạy container mới, ánh xạ port 8080 của máy chủ vào 8080 của container
                sh 'docker run -d -p 8080:8080 --name my-running-app ${IMAGE_NAME}:latest'
            }
        }
    }

    post {
        always {
            echo 'Pipeline finished.'
            // Dọn dẹp các dangling images để tránh đầy ổ cứng
            sh 'docker image prune -f || true'
        }
        success {
            echo 'Docker image built and deployed successfully! ✅'
        }
        failure {
            echo 'Pipeline failed! ❌ Vui lòng kiểm tra lại log.'
        }
    }
}
