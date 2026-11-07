import ctypes,ctypes.util,json,pathlib,re
root=pathlib.Path(r'C:/Users/Edgar.DESKTOP-BOONDKL/.codex/sessions/2026/10/18')
lib=ctypes.CDLL(r'C:/Program Files/Git/mingw64/bin/libzstd.dll')
class Buffer(ctypes.Structure):_fields_=[('src',ctypes.c_void_p),('size',ctypes.c_size_t),('pos',ctypes.c_size_t)]
lib.ZSTD_createDStream.restype=ctypes.c_void_p
lib.ZSTD_initDStream.argtypes=[ctypes.c_void_p];lib.ZSTD_decompressStream.argtypes=[ctypes.c_void_p,ctypes.POINTER(Buffer),ctypes.POINTER(Buffer)]
lib.ZSTD_freeDStream.argtypes=[ctypes.c_void_p]
def read(p):
 if p.suffix!='.zst':return p.read_text(encoding='utf-8')
 raw=p.read_bytes();src=ctypes.create_string_buffer(raw);ib=Buffer(ctypes.cast(src,ctypes.c_void_p),len(raw),0);stream=lib.ZSTD_createDStream();lib.ZSTD_initDStream(stream);parts=[]
 while ib.pos<ib.size:
  dst=ctypes.create_string_buffer(4*1024*1024);ob=Buffer(ctypes.cast(dst,ctypes.c_void_p),len(dst),0);code=lib.ZSTD_decompressStream(stream,ctypes.byref(ob),ctypes.byref(ib));
  if code>2**63:raise RuntimeError(code)
  parts.append(dst.raw[:ob.pos])
 lib.ZSTD_freeDStream(stream);return b''.join(parts).decode('utf-8')
if __name__=='__main__':
 patches=[];counts={}
 for p in root.glob('*01a150f5*'):
  data=read(p)
  for line in data.splitlines():
   try:o=json.loads(line)
   except:continue
   payload=o.get('payload',{});s=json.dumps(payload,ensure_ascii=False)
   if payload.get('type') in ('function_call','custom_tool_call') and '*** Update File:' in s:
    patches.append({'time':o.get('timestamp'),'file':p.name,'payload':payload})
    for name in re.findall(r'\*\*\* Update File: ([^\\\n"]+)',s):counts[name]=counts.get(name,0)+1
 pathlib.Path('Artifacts/TelltaleGameBuild/historical-patches.json').write_text(json.dumps(patches,ensure_ascii=False),encoding='utf-8')
 print(json.dumps(counts,indent=2));print('patch calls',len(patches))
