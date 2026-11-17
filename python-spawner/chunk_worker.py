"""Single daemon worker: preserve warm generation caches without delaying exit."""
import queue
import threading
from concurrent.futures import Future


class ChunkWorker:
 def __init__(self):
  self._queue=queue.Queue()
  self._lock=threading.Lock()
  self._closed=False
  self._thread=None

 def submit(self,fn,*args):
  with self._lock:
   if self._closed: raise RuntimeError('Chunk worker is stopped')
   future=Future()
   self._queue.put((future,fn,args))
   if self._thread is None:
    self._thread=threading.Thread(target=self._run,name='world-chunks',daemon=True)
    self._thread.start()
   return future

 def _run(self):
  while True:
   item=self._queue.get()
   if item is None: return
   future,fn,args=item
   if not future.set_running_or_notify_cancel(): continue
   try: result=fn(*args)
   except BaseException as error: future.set_exception(error)
   else: future.set_result(result)

 def shutdown(self,wait=False,cancel_futures=True):
  with self._lock:
   if not self._closed:
    self._closed=True
    if cancel_futures:
     while True:
      try: item=self._queue.get_nowait()
      except queue.Empty: break
      if item is not None: item[0].cancel()
    self._queue.put(None)
  if wait and self._thread is not None: self._thread.join()
