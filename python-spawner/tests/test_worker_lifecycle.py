import subprocess
import sys
from pathlib import Path


def test_shutdown_exits_without_waiting_for_running_generation():
 script = '''
import chunks, threading
from settings import Settings
from region import MILE
started=threading.Event()
def slow(*args):
 started.set()
 threading.Event().wait(60)
 return '<svg/>'
chunks.generate=slow
chunks.CACHE=__import__('pathlib').Path(__import__('tempfile').mkdtemp())
chunks.request(dict(seed=31,window=[0,0,MILE,MILE]),Settings())
assert started.wait(2)
chunks.shutdown()
print('stopped',flush=True)
'''
 result=subprocess.run([sys.executable,'-c',script],cwd=Path(__file__).resolve().parents[1],capture_output=True,text=True,timeout=5)
 assert result.returncode==0,result.stderr
 assert result.stdout.strip()=='stopped'
 assert not result.stderr


def test_submission_failure_does_not_leave_active_job_stuck(monkeypatch,tmp_path):
 import chunks
 from settings import Settings
 from region import MILE
 class ClosedPool:
  def submit(self,*args): raise RuntimeError('worker unavailable')
 monkeypatch.setattr(chunks,'POOL',ClosedPool())
 monkeypatch.setattr(chunks,'JOBS',{})
 monkeypatch.setattr(chunks,'WAITING',{})
 monkeypatch.setattr(chunks,'ACTIVE',None)
 monkeypatch.setattr(chunks,'CACHE',tmp_path)
 import pytest
 with pytest.raises(RuntimeError,match='worker unavailable'):
  chunks.request(dict(seed=31,window=[0,0,MILE,MILE]),Settings())
 assert chunks.ACTIVE is None


def test_status_identifies_worker_busy_with_previous_seed(monkeypatch,tmp_path):
 import chunks
 from concurrent.futures import Future
 from settings import Settings
 from region import MILE
 class BusyPool:
  def submit(self,*args): return Future()
 monkeypatch.setattr(chunks,'POOL',BusyPool())
 monkeypatch.setattr(chunks,'JOBS',{})
 monkeypatch.setattr(chunks,'WAITING',{})
 monkeypatch.setattr(chunks,'ACTIVE',None)
 monkeypatch.setattr(chunks,'CACHE',tmp_path)
 chunks.request(dict(seed=31,window=[0,0,MILE,MILE]),Settings())
 result=chunks.request(dict(seed=13,window=[0,0,MILE,MILE]),Settings())
 assert result['active'] is None
 assert result['worker']['seed']==31
 assert result['worker']['x']==result['worker']['y']==0
 assert result['worker']['elapsed_seconds']>=0


def test_server_keyboard_interrupt_closes_worker_before_exit():
 script = '''
import chunks, control, threading, tempfile
from pathlib import Path
from settings import Settings
from region import MILE
started=threading.Event()
def slow(*args):
 started.set()
 threading.Event().wait(60)
 return '<svg/>'
chunks.generate=slow
chunks.CACHE=Path(tempfile.mkdtemp())
class InterruptedServer:
 server_port=0
 def __init__(self,*args): pass
 def __enter__(self): return self
 def __exit__(self,*args): pass
 def serve_forever(self):
  chunks.request(dict(seed=31,window=[0,0,MILE,MILE]),Settings())
  assert started.wait(2)
  raise KeyboardInterrupt
control.ThreadingHTTPServer=InterruptedServer
control.serve()
assert chunks.STOPPING and not chunks.WAITING
print('server stopped',flush=True)
'''
 result=subprocess.run([sys.executable,'-c',script],cwd=Path(__file__).resolve().parents[1],capture_output=True,text=True,timeout=5)
 assert result.returncode==0,result.stderr
 assert 'server stopped' in result.stdout
 assert not result.stderr


def test_worker_shutdown_cancels_queued_jobs_and_rejects_new_work():
 import threading
 from chunk_worker import ChunkWorker
 import pytest
 worker=ChunkWorker(); started=threading.Event(); release=threading.Event()
 def slow():
  started.set(); release.wait(5)
 first=worker.submit(slow)
 try:
  assert started.wait(2)
  queued=worker.submit(lambda:None)
  worker.shutdown()
  assert queued.cancelled()
  with pytest.raises(RuntimeError,match='stopped'): worker.submit(lambda:None)
 finally:
  release.set(); first.result(timeout=2); worker.shutdown(wait=True)
