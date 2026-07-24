# Cấu hình NGINX và Tailscale Funnel (cho Webhook & Truy cập Internet)

Tài liệu này ghi chú lại cách mở ứng dụng (hoặc Jenkins) ra Internet bằng mạng Tailscale Funnel kết hợp với NGINX làm Reverse Proxy, giúp tránh tuyệt đối lỗi giao thức SSL (`ERR_SSL_PROTOCOL_ERROR`).

## 1. Bản chất vấn đề
- **Tailscale Funnel** làm nhiệm vụ đứng ở vòng ngoài (Internet), nó đã tự động xử lý toàn bộ quá trình mã hoá và cấp phát chứng chỉ SSL (HTTPS) cho tên miền `.ts.net`.
- Khi dữ liệu đi xuyên qua Funnel vào máy chủ nội bộ của bạn, nó đã được giải mã thành **Plain HTTP** (dữ liệu thuần).
- Do đó, **NGINX ở bên trong KHÔNG ĐƯỢC bật cấu hình SSL (listen 443 ssl)** nữa. Nó chỉ cần lắng nghe ở port 80 (HTTP bình thường) để hứng dữ liệu từ Funnel đưa vào.

## 2. File cấu hình NGINX chuẩn
Đường dẫn gợi ý trên Linux: `/etc/nginx/sites-available/project3.conf`

```nginx
server {
    listen 80;
    
    # Đổi thành tên miền Tailscale của bạn
    server_name server.tail2d141f.ts.net;

    location / {
        # Trỏ vào port của ứng dụng đang chạy (ví dụ Docker container ở port 5000)
        proxy_pass http://127.0.0.1:5000;
        
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        
        # Header cực kỳ quan trọng: Báo cho App (hoặc Jenkins) biết rằng người dùng ở ngoài Internet thực chất đang kết nối bằng HTTPS
        proxy_set_header X-Forwarded-Proto https;
    }
}
```

## 3. Các bước khởi chạy
1. **Kiểm tra cú pháp và khởi động lại NGINX**:
   ```bash
   sudo nginx -t
   sudo systemctl reload nginx
   ```
2. **Khởi động Tailscale Funnel (Trỏ vào port 80 của NGINX)**:
   ```bash
   tailscale funnel 80
   ```
   *(Lệnh này cần được giữ chạy trên terminal, hoặc cấu hình chạy ngầm bằng `screen`, `tmux`, hoặc biến nó thành systemd service để không bị tắt khi đóng terminal).*

## 4. Xử lý lỗi thường gặp (Troubleshooting) với GitHub Webhook
- **GitHub báo lỗi Timeout / 302 Found**: Hãy chắc chắn Payload URL trên GitHub có **dấu gạch chéo `/` ở cuối cùng** (Ví dụ: `https://.../github-webhook/`).
- **GitHub báo lỗi 404 Not Found**: Đảm bảo Funnel hoặc NGINX đang trỏ request của GitHub về đúng port đang chạy **Jenkins** (thường là 8080) chứ không phải trỏ nhầm vào port của App (5000).
- **GitHub báo lỗi 403 Forbidden**: Webhook đã vào đến Jenkins nhưng bị hệ thống bảo vệ chặn lại. Hãy vào **Jenkins -> Manage Jenkins -> Security -> Tắt tính năng CSRF Protection** (Prevent Cross Site Request Forgery exploits) hoặc cấu hình cho phép Webhook đi qua.
- **Lỗi ERR_SSL_PROTOCOL_ERROR (Trên trình duyệt)**: Do bạn cố tình gắn chứng chỉ SSL vào NGINX bên trong máy chủ. Hãy xoá khối lệnh `listen 443 ssl;` đi và làm theo đúng mục số 2 ở trên!
