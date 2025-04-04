﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UseCase.MailService
{
    public static class HtmlHelper
    {

        public static string GenerateHTMLNotification(List<string> product, string Name, string phone, string email)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(@$"<!DOCTYPE html>
<html>
<head>
  <meta charset=""UTF-8"">
  <title>Thông báo Đặt Hàng Mới</title>
  <style>
    body {{
      font-family: Arial, sans-serif;
      background-color: #f5f5f5;
      margin: 0;
      padding: 20px;
    }}
    .container {{
      background-color: #ffffff;
      max-width: 600px;
      margin: 0 auto;
      border-radius: 5px;
      box-shadow: 0 0 10px rgba(0, 0, 0, 0.1);
      overflow: hidden;
    }}
    .header {{
      background-color: #007BFF;
      color: #ffffff;
      padding: 20px;
      text-align: center;
    }}
    .content {{
      padding: 20px;
    }}
    .info {{
      margin-bottom: 15px;
      line-height: 1.6;
    }}
    .info span {{
      font-weight: bold;
    }}
    .footer {{
      background-color: #f1f1f1;
      text-align: center;
      font-size: 12px;
      color: #777777;
      padding: 10px;
    }}
  </style>
</head>
<body>
  <div class=""container"">
    <div class=""header"">
      <h2>Thông báo Đặt Hàng Mới</h2>
    </div>
    <div class=""content"">
      <p>Chào Bạn,</p>
      <p>Đã có một khách hàng vừa đặt mua sản phẩm. Dưới đây là thông tin chi tiết:</p>
      <div class=""info"">
        <span>Tên khách hàng:</span> {Name}
      </div>
      <div class=""info"">
        <span>Số điện thoại:</span> {phone}
      </div>
      <div class=""info"">
        <span>Email:</span> {email}
      </div>
      <div class=""info"">
        <span>Thông tin sản phẩm:</span>
<ul>");
            foreach (var item in product)
            {
                sb.Append("<li>" + item + "</li>");
            }
            sb.Append(@$"</ul>
      </div>
      <p>Vui lòng kiểm tra và xác nhận đơn hàng.</p>
    </div>
    <div class=""footer"">
      © 2025 TruongShop.net. All rights reserved.
    </div>
  </div>
</body>
</html>
");
            return sb.ToString();
        }

        public static string GenerateHTMLContent(string pass)
        {
            return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Your Password</title>
    <style>
        body {{
            font-family: Arial, sans-serif;
            background-color: #f4f4f9;
            margin: 0;
            padding: 0;
            display: flex;
            justify-content: center;
            align-items: center;
            height: 100vh;
        }}

        .container {{
            background-color: #ffffff;
            width: 100%;
            max-width: 400px;
            padding: 30px;
            border-radius: 10px;
            box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
            text-align: center;
        }}

        .container h1 {{
            font-size: 28px;
            color: #333333;
            margin-bottom: 20px;
        }}

        .container p {{
            font-size: 16px;
            color: #666666;
            margin-bottom: 20px;
        }}

        .password-box {{
            font-size: 18px;
            color: #007bff;
            background-color: #f0f8ff;
            padding: 10px 20px;
            border-radius: 5px;
            display: inline-block;
            margin-bottom: 20px;
            border: 1px solid #007bff;
        }}

        .footer {{
            font-size: 12px;
            color: #999999;
            margin-top: 20px;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <h1>Your Password</h1>
        <p>Below is your new password. Please keep it secure and change it after logging in.</p>
        <div class=""password-box"">{pass}</div>
        <p>If you have any issues, please contact our support team.</p>
        <div class=""footer"">
            &copy; 2025 Secure Service. All rights reserved.
        </div>
    </div>
</body>
</html>
";
        }

        public static string GenerateHTMLContent(List<string> items, bool isPaid)
        {
            var sb = new StringBuilder();

            // Phần đầu của HTML
            sb.Append(@"<!DOCTYPE html>
<html lang=""vi"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
  <title>" + (isPaid ? "Thông Báo Thanh Toán Thành Công" : "Xác Nhận Đơn Hàng Thành Công") + @"</title>
  <style>
    body {
      margin: 0;
      font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
      background: #f4f7f9;
      color: #333;
      padding: 20px;
    }
    .email-container {
      background: #fff;
      max-width: 600px;
      margin: auto;
      padding: 30px;
      border-radius: 8px;
      box-shadow: 0 0 20px rgba(0, 0, 0, 0.1);
    }
    .header {
      text-align: center;
      margin-bottom: 30px;
    }
    .header h1 {
      color: #4CAF50;
    }
    .content p {
      font-size: 1.1em;
      line-height: 1.6;
      margin: 20px 0;
    }
    .info-box {
      border-top: 1px solid #ddd;
      padding-top: 20px;
      margin-top: 30px;
    }
    .info-box h2 {
      font-size: 1.3em;
      margin-bottom: 15px;
      color: #333;
    }
    .contact-item {
      margin-bottom: 10px;
      display: flex;
      align-items: center;
    }
    .contact-item span {
      margin-left: 10px;
      font-size: 1em;
    }
    .contact-icon {
      width: 24px;
      height: 24px;
    }
    .btn {
      display: inline-block;
      margin-top: 20px;
      padding: 12px 25px;
      background: #4CAF50;
      color: #fff;
      text-decoration: none;
      border-radius: 5px;
      transition: background 0.3s ease;
    }
    .btn:hover {
      background: #43a047;
    }
  </style>
  <link rel=""stylesheet"" href=""https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css"">
</head>
<body>
  <div class=""email-container"">
    <div class=""header"">
      <h1>TruongShop Xin Chào Quý Khách!" + @"</h1>
    </div>
    <div class=""content"">
      <p>" + (isPaid
                ? "Chúng tôi xin thông báo rằng giao dịch thanh toán của bạn đã được xử lý thành công. Cảm ơn bạn đã tin tưởng và sử dụng dịch vụ của chúng tôi."
                : "Chúng tôi xin thông báo rằng giao dịch đơn hàng của bạn đã được xử lý thành công. Vui lòng gửi ảnh thông tin đã chuyển khoản thành công để nhận sản phẩm.") + @"</p>
    </div>
    
    <!-- Phần hiển thị danh sách -->
    <div class=""list-content"">
      <h2>Sản phẩm đã mua:</h2>
      <ul>");

            // Lặp qua danh sách và thêm các mục vào HTML
            foreach (var item in items)
            {
                sb.Append("<li>" + item + "</li>");
            }

            sb.Append(@"</ul>
    </div>
    
    <div class=""info-box"">
      <h2>Thông Tin Liên Hệ Người Bán</h2>
      <div class=""contact-item"">
        <i class=""fa fa-phone"" aria-hidden=""true""></i>
        <span>Số điện thoại: 0358 223 929</span>
      </div>
      <div class=""contact-item"">
        <i class=""fa fa-facebook-square"" aria-hidden=""true""></i>
        <span>Facebook: <a href=""https://www.facebook.com/truong.luong.386820"" target=""_blank"">facebook.com</a></span>
      </div>
      <div class=""contact-item"">
        <i class=""fa fa-comments"" aria-hidden=""true""></i>
        <span>Zalo: 0358 223 929</span>
      </div>
    </div>
  </div>
</body>
</html>");

            return sb.ToString();
        }
    }
}
