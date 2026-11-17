"""Optional per-request observation; never part of a generator/cache key."""
from contextlib import contextmanager
from contextvars import ContextVar

_OBSERVER=ContextVar('generation_observer',default=None)

@contextmanager
def listen(observer):
 token=_OBSERVER.set(observer)
 try: yield
 finally: _OBSERVER.reset(token)

def observing():
 return _OBSERVER.get() is not None

def publish(stage,fraction=None,**snapshot):
 observer=_OBSERVER.get()
 if observer is not None: observer(stage,fraction,**snapshot)
