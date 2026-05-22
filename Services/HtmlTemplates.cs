namespace LanTransfer.Services;

public static class HtmlTemplates
{
    public static string EnterCodePage => @"<!DOCTYPE html>
<html lang=""zh-CN"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>局域网文件传输</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body { font-family: -apple-system, 'Segoe UI', sans-serif; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); min-height: 100vh; display: flex; align-items: center; justify-content: center; }
        .card { background: white; border-radius: 16px; padding: 40px; box-shadow: 0 20px 60px rgba(0,0,0,0.3); text-align: center; max-width: 400px; width: 90%; }
        h1 { font-size: 24px; margin-bottom: 8px; color: #333; }
        p { color: #666; margin-bottom: 24px; }
        input { width: 100%; padding: 14px; font-size: 24px; text-align: center; border: 2px solid #ddd; border-radius: 12px; outline: none; letter-spacing: 8px; }
        input:focus { border-color: #667eea; }
        button { width: 100%; padding: 14px; font-size: 16px; background: linear-gradient(135deg, #667eea, #764ba2); color: white; border: none; border-radius: 12px; cursor: pointer; margin-top: 16px; }
        button:hover { opacity: 0.9; }
    </style>
</head>
<body>
    <div class=""card"">
        <h1>局域网文件传输</h1>
        <p>请输入 4 位连接码</p>
        <form method=""GET"" action=""/"">
            <input type=""text"" name=""code"" maxlength=""4"" pattern=""\d{4}"" placeholder=""0000"" autofocus required>
            <button type=""submit"">连接</button>
        </form>
    </div>
</body>
</html>";

    public static string MainPage(string code)
    {
        return @"<!DOCTYPE html>
<html lang=""zh-CN"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>局域网文件传输</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body { font-family: -apple-system, 'Segoe UI', sans-serif; background: #f0f2f5; min-height: 100vh; padding: 20px; }
        .container { max-width: 600px; margin: 0 auto; }
        h1 { text-align: center; color: #333; margin-bottom: 24px; font-size: 24px; }
        .card { background: white; border-radius: 12px; padding: 24px; margin-bottom: 16px; box-shadow: 0 2px 8px rgba(0,0,0,0.1); }
        .card h2 { font-size: 16px; color: #666; margin-bottom: 16px; }
        .drop-zone { border: 2px dashed #667eea; border-radius: 12px; padding: 40px; text-align: center; color: #667eea; cursor: pointer; transition: all 0.3s; }
        .drop-zone:hover, .drop-zone.dragover { background: #f0f2ff; border-color: #764ba2; }
        .drop-zone input { display: none; }
        .progress { display: none; margin-top: 16px; }
        .progress .bar { height: 8px; background: #e9ecef; border-radius: 4px; overflow: hidden; }
        .progress .bar .fill { height: 100%; background: linear-gradient(90deg, #667eea, #764ba2); width: 0%; transition: width 0.3s; }
        .progress .text { font-size: 14px; color: #666; margin-top: 8px; text-align: center; }
        .file-list { list-style: none; }
        .file-list li { padding: 12px; border-bottom: 1px solid #f0f0f0; display: flex; justify-content: space-between; align-items: center; }
        .file-list li:last-child { border-bottom: none; }
        .file-name { font-weight: 500; color: #333; word-break: break-all; }
        .file-meta { color: #999; font-size: 13px; white-space: nowrap; margin-left: 12px; }
        .btn { display: inline-block; padding: 8px 20px; background: linear-gradient(135deg, #667eea, #764ba2); color: white; border: none; border-radius: 8px; cursor: pointer; text-decoration: none; font-size: 14px; }
        .btn:hover { opacity: 0.9; }
        .msg { padding: 12px; border-radius: 8px; margin-top: 12px; display: none; }
        .msg.success { background: #d4edda; color: #155724; display: block; }
        .msg.error { background: #f8d7da; color: #721c24; display: block; }
    </style>
</head>
<body>
    <div class=""container"">
        <h1>局域网文件传输</h1>

        <div class=""card"">
            <h2>上传文件到对方电脑</h2>
            <div class=""drop-zone"" id=""dropZone"" onclick=""document.getElementById('fileInput').click()"">
                <p style=""font-size:48px;margin-bottom:8px"">+</p>
                <p>点击选择或拖拽文件到此处</p>
                <input type=""file"" id=""fileInput"" multiple>
            </div>
            <div class=""progress"" id=""progress"">
                <div class=""bar""><div class=""fill"" id=""progressFill""></div></div>
                <div class=""text"" id=""progressText"">准备中...</div>
            </div>
            <div class=""msg"" id=""msg""></div>
        </div>

        <div class=""card"">
            <h2>可下载的文件</h2>
            <ul class=""file-list"" id=""fileList"">
                <li style=""color:#999;text-align:center;display:block"">暂无共享文件</li>
            </ul>
        </div>
    </div>

    <script>
        var code = '" + code + @"';
        var dropZone = document.getElementById('dropZone');
        var fileInput = document.getElementById('fileInput');
        var progress = document.getElementById('progress');
        var progressFill = document.getElementById('progressFill');
        var progressText = document.getElementById('progressText');
        var msg = document.getElementById('msg');

        dropZone.addEventListener('dragover', function(e) { e.preventDefault(); dropZone.classList.add('dragover'); });
        dropZone.addEventListener('dragleave', function() { dropZone.classList.remove('dragover'); });
        dropZone.addEventListener('drop', function(e) { e.preventDefault(); dropZone.classList.remove('dragover'); uploadFiles(e.dataTransfer.files); });
        fileInput.addEventListener('change', function() {
            if (fileInput.files.length > 0) uploadFiles(fileInput.files);
            fileInput.value = '';
        });

        function uploadFiles(files) {
            if (!files || !files.length) return;
            for (var i = 0; i < files.length; i++) uploadFile(files[i]);
        }

        function uploadFile(file) {
            var formData = new FormData();
            formData.append('file', file);

            var xhr = new XMLHttpRequest();
            progress.style.display = 'block';
            msg.className = 'msg';
            msg.style.display = 'none';

            xhr.upload.addEventListener('progress', function(e) {
                if (e.lengthComputable) {
                    var pct = Math.round(e.loaded / e.total * 100);
                    progressFill.style.width = pct + '%';
                    progressText.textContent = pct + '% - ' + formatSize(e.loaded) + ' / ' + formatSize(e.total);
                }
            });

            xhr.addEventListener('load', function() {
                if (xhr.status === 200) {
                    msg.className = 'msg success';
                    msg.textContent = '上传成功: ' + file.name;
                    loadFileList();
                } else {
                    msg.className = 'msg error';
                    msg.textContent = '上传失败 (' + xhr.status + '): ' + xhr.responseText;
                }
                msg.style.display = 'block';
                progress.style.display = 'none';
                progressFill.style.width = '0%';
            });

            xhr.addEventListener('error', function() {
                msg.className = 'msg error';
                msg.textContent = '网络错误';
                msg.style.display = 'block';
                progress.style.display = 'none';
            });

            xhr.open('POST', '/upload?code=' + code);
            xhr.send(formData);
        }

        function escapeHtml(text) {
            var div = document.createElement('div');
            div.appendChild(document.createTextNode(text));
            return div.innerHTML;
        }

        function updateFileList(files) {
            var list = document.getElementById('fileList');
            if (!files.length) {
                list.innerHTML = '<li style=""color:#999;text-align:center;display:block"">暂无共享文件</li>';
                return;
            }
            var html = '';
            for (var i = 0; i < files.length; i++) {
                var f = files[i];
                html += '<li><span class=""file-name"">' + escapeHtml(f.name) + '</span><span class=""file-meta"">' + f.size + ' <a class=""btn"" href=""/download/' + f.id + '?code=' + code + '"" style=""margin-left:8px"">下载</a></span></li>';
            }
            list.innerHTML = html;
        }

        function loadFileList() {
            fetch('/files?code=' + code)
                .then(function(r) { return r.json(); })
                .then(updateFileList);
        }

        function formatSize(bytes) {
            if (bytes < 1024) return bytes + ' B';
            if (bytes < 1024*1024) return (bytes/1024).toFixed(1) + ' KB';
            return (bytes/(1024*1024)).toFixed(1) + ' MB';
        }

        loadFileList();

        // SSE 实时更新文件列表
        var evtSource = new EventSource('/events?code=' + code);
        evtSource.onmessage = function(e) {
            try { updateFileList(JSON.parse(e.data)); } catch {}
        };
        evtSource.onerror = function() {
            evtSource.close();
            // 断线后回退到轮询
            setInterval(loadFileList, 3000);
        };
    </script>
</body>
</html>";
    }

    public static string ErrorPage(string message)
    {
        return @"<!DOCTYPE html>
<html lang=""zh-CN"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>错误</title>
    <style>
        body { font-family: -apple-system, 'Segoe UI', sans-serif; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); min-height: 100vh; display: flex; align-items: center; justify-content: center; }
        .card { background: white; border-radius: 16px; padding: 40px; text-align: center; box-shadow: 0 20px 60px rgba(0,0,0,0.3); }
        h1 { font-size: 48px; margin-bottom: 16px; }
        p { color: #666; font-size: 18px; }
        a { color: #667eea; text-decoration: none; margin-top: 16px; display: inline-block; }
    </style>
</head>
<body>
    <div class=""card"">
        <h1>:(</h1>
        <p>" + message + @"</p>
        <a href=""/"">返回首页</a>
    </div>
</body>
</html>";
    }
}
