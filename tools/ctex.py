import sys, struct
d=open(sys.argv[1],'rb').read()
if d[:4]==b'GST2':
    ver,w,h,df = struct.unpack_from('<IIII', d, 4)
    print("ctex v%d  %dx%d  dataformat=0x%x  bytes=%d" % (ver,w,h,df,len(d)))
elif d[:8]==b'\x89PNG\r\n\x1a\n':
    w,h = struct.unpack_from('>II', d, 16)
    print("png %dx%d bytes=%d" % (w,h,len(d)))
else:
    print("unknown magic %r" % d[:8])
