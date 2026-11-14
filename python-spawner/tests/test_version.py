import threading, urllib.request
from http.server import HTTPServer
from pathlib import Path
from control import Handler

def test_page_html_contains_version_text():
 version=(Path(__file__).resolve().parents[1]/'VERSION').read_text(encoding='utf-8').strip()
 with HTTPServer(('127.0.0.1',0),Handler) as server:
  thread=threading.Thread(target=server.serve_forever,daemon=True); thread.start()
  try:
   html=urllib.request.urlopen('http://127.0.0.1:'+str(server.server_port)).read().decode()
  finally:
   server.shutdown(); thread.join()
 assert version in html
