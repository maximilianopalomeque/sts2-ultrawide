import sys, struct

data = open(sys.argv[1], 'rb').read()
assert data[:4] == b'ECFG', data[:4]

def u32(p): return struct.unpack_from('<I', data, p)[0]
def pad4(n): return (4 - (n % 4)) % 4

def decode(p):
    hdr = u32(p); p += 4
    t, f = hdr & 0xFFFF, hdr >> 16
    if t == 0: return None
    if t == 1: return u32(p) != 0
    if t == 2: return struct.unpack_from('<q' if f & 1 else '<i', data, p)[0]
    if t == 3: return struct.unpack_from('<d' if f & 1 else '<f', data, p)[0]
    if t in (4, 21, 22):
        n = u32(p); p += 4
        return data[p:p+n].rstrip(b'\x00').decode('utf-8', 'replace')
    if t == 5: return struct.unpack_from('<2f', data, p)
    if t == 6: return struct.unpack_from('<2i', data, p)
    if t == 20: return struct.unpack_from('<4f', data, p)
    return '<variant type %d>' % t

count = u32(4)
pos = 8
res = {}
for _ in range(count):
    kl = u32(pos); pos += 4
    key = data[pos:pos+kl].decode('utf-8', 'replace'); pos += kl
    vl = u32(pos); pos += 4
    res[key] = decode(pos); pos += vl

pat = sys.argv[2] if len(sys.argv) > 2 else ''
print("# %d settings parsed, %d bytes consumed of %d" % (count, pos, len(data)))
for k in sorted(res):
    if pat.lower() in k.lower():
        print("%-62s = %r" % (k, res[k]))
