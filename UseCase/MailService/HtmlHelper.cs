using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UseCase.MailService
{
    public static class HtmlHelper
    {
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
    }
}
