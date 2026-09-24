import csv,sys,collections
rows=list(csv.DictReader(open(sys.argv[1],encoding='utf-8')))
V=[]; d={}
for r in rows:
    if r['variant'] not in V: V.append(r['variant'])
    d[(r['variant'],r['cell'])]=r
print('LOSS% (hic/acg/man)')
print('cell'.ljust(10)+''.join(v[:12].rjust(20) for v in V))
for P in (1,2,3,4):
  for pr in ('zayif','orta','iyi'):
    line=f'P{P}_{pr}'.ljust(10)
    for v in V:
      line+=('/'.join(f"{float(d[(v,f'P{P}_{pr}_{s}')]['loss%']):.0f}" for s in ('hic','acgozlu','mantikli'))).rjust(20)
    print(line)
print('\nFINAL mean (hic/acg/man)')
for P in (1,2,3,4):
  for pr in ('zayif','orta','iyi'):
    line=f'P{P}_{pr}'.ljust(10)
    for v in V:
      line+=('/'.join(f"{int(d[(v,f'P{P}_{pr}_{s}')]['final_mean'])}" for s in ('hic','acgozlu','mantikli'))).rjust(20)
    print(line)
print('\nCARDS (acg/man)')
for P in (1,3,4):
  for pr in ('orta','iyi'):
    line=f'P{P}_{pr}'.ljust(10)
    for v in V:
      line+=('/'.join(f"{float(d[(v,f'P{P}_{pr}_{s}')]['cards']):.1f}" for s in ('acgozlu','mantikli'))).rjust(20)
    print(line)
