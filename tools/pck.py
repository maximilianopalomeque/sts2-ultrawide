import sys, struct, os

class PCK:
    def __init__(self, path):
        self.path = path
        self.f = open(path, 'rb')
        self.parse()

    def rd(self, n):
        return self.f.read(n)
    def u32(self):
        return struct.unpack('<I', self.rd(4))[0]
    def u64(self):
        return struct.unpack('<Q', self.rd(8))[0]

    def parse(self):
        f = self.f
        magic = self.rd(4)
        if magic != b'GDPC':
            raise SystemExit("not a pck, magic=%r" % magic)
        self.fmt = self.u32()
        self.vmaj, self.vmin, self.vpat = self.u32(), self.u32(), self.u32()
        self.flags = 0
        self.file_base = 0
        self.dir_offset = None
        if self.fmt >= 2:
            self.flags = self.u32()
            self.file_base = self.u64()
        if self.fmt >= 3:
            # format 3 moves the directory to its own offset (usually EOF)
            self.dir_offset = self.u64()
            f.seek(self.dir_offset)
        else:
            self.rd(16*4)
        self.count = self.u32()
        self.files = []
        for _ in range(self.count):
            plen = self.u32()
            raw = self.rd(plen)
            p = raw.rstrip(b'\x00').decode('utf-8', 'replace')
            off = self.u64()
            size = self.u64()
            md5 = self.rd(16)
            fl = self.u32() if self.fmt >= 2 else 0
            self.files.append((p, off + self.file_base, size, fl))

    def extract(self, name, dest):
        for p, off, size, fl in self.files:
            if p == name:
                self.f.seek(off)
                data = self.f.read(size)
                os.makedirs(os.path.dirname(dest), exist_ok=True)
                open(dest, 'wb').write(data)
                return size
        return None

if __name__ == '__main__':
    pck = PCK(sys.argv[1])
    cmd = sys.argv[2] if len(sys.argv) > 2 else 'info'
    if cmd == 'info':
        print("format=%d godot=%d.%d.%d flags=0x%x file_base=%d count=%d" % (
            pck.fmt, pck.vmaj, pck.vmin, pck.vpat, pck.flags, pck.file_base, pck.count))
    elif cmd == 'list':
        pat = sys.argv[3] if len(sys.argv) > 3 else ''
        for p, off, size, fl in pck.files:
            if pat in p:
                print("%12d  %s%s" % (size, p, "  [ENC]" if fl & 1 else ""))
    elif cmd == 'tree':
        from collections import Counter
        c = Counter()
        sz = Counter()
        for p, off, size, fl in pck.files:
            parts = p.replace('res://','').split('/')
            key = '/'.join(parts[:2]) if len(parts) > 1 else parts[0]
            c[key] += 1
            sz[key] += size
        for k, n in c.most_common(60):
            print("%6d files %10.1f MB  %s" % (n, sz[k]/1048576, k))
    elif cmd == 'get':
        n = pck.extract(sys.argv[3], sys.argv[4])
        print("extracted %s bytes -> %s" % (n, sys.argv[4]))
