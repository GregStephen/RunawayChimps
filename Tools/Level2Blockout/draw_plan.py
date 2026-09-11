"""Draw the exact v0.4 top-down Level 2 blockout from layout.json."""
from pathlib import Path
import json,sys
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.patches import Rectangle,FancyArrowPatch
HERE=Path(__file__).resolve().parent
OUT=Path(sys.argv[1]) if len(sys.argv)>1 else HERE
OUT.mkdir(parents=True,exist_ok=True)
d=json.loads((HERE/'layout.json').read_text()); nodes=d['nodes']; byid={n['id']:n for n in nodes}
fig,ax=plt.subplots(figsize=(12,12),dpi=170); fig.patch.set_facecolor('#f4f3ef'); ax.set_facecolor('#f4f3ef')
fig.subplots_adjust(left=.05,right=.96,top=.90,bottom=.06)
fig.text(.06,.965,'RUNAWAY CHIMPS',fontsize=12,weight='bold',color='#657777')
fig.text(.06,.93,'Level 2 · Behavioral Conditioning',fontsize=24,weight='bold',color='#1c343d')
fig.text(.06,.902,'Expanded blockout v0.4  /  minimum 36 × 36 m footprint',fontsize=11,color='#516469')
palette={'SafeFloor':'#c1dbcf','Floor':'#e2dfd3','RepairFloor':'#efdbb9','BypassFloor':'#cbd9df'}
for name,x0,x1,z0,z1,mat in d['rooms']:
 ax.add_patch(Rectangle((x0,z0),x1-x0,z1-z0,facecolor='#b5dbe2' if name=='Optional_Reward_Room' else palette[mat],ec='none',zorder=1))
for n in nodes:
 if not n['material'] or not n['active']:continue
 parent=byid[n['parent']]['name'] if n['parent'] else ''
 x,y,z=n['position']; sx,sy,sz=n['scale']
 if parent.startswith('02_') and y-sy/2<1:ax.add_patch(Rectangle((x-sx/2,z-sz/2),sx,sz,fc='#2e444a',zorder=7))
 if parent.startswith('04_'):ax.add_patch(Rectangle((x-sx/2,z-sz/2),sx,sz,fc='#82989e',ec='#334d56',lw=1.2,zorder=5))
labels=[
 (3,-2,'SAFE ENTRY\n6 × 4 m'),(9,7,'TEST HALL A\n18 × 14 m'),(24,7,'LOWER SERVICE\n12 × 14 m'),
 (4,19,'WEST OBSERVATION\n8 × 10 m'),(19,19,'CONDITIONING HALL B\n22 × 10 m'),(33,13,'EAST BYPASS\n6 × 22 m'),
 (4,25,'EXIT APPROACH'),(4,29,'COMPLETED EXIT\nSAFE'),(14,28,'NORTH GALLERY\n12 × 8 m'),
 (28,28,'REPAIR LAB\n16 × 8 m'),(33,-1,'LOCKED REWARD\n6 × 6 m')]
for x,z,t in labels:ax.text(x,z,t,ha='center',va='center',fontsize=9.5,fontweight='bold',color='#213c45',zorder=10)
ax.text(28,30.7,'NOISY REPAIR',ha='center',fontsize=8.5,color='#8e5c20',weight='bold')
pts=[(28,28),(20.8,28),(15,27),(15,20),(25,20),(33,19),(33,27),(28,28)]
ax.plot(*zip(*pts),ls=(0,(5,4)),color='#ae742f',lw=1.8,zorder=4)
for a,b in [((21,28),(19.8,28)),((18,20),(19.5,20)),((33,22),(33,23.4))]:
 ax.add_patch(FancyArrowPatch(a,b,arrowstyle='-|>',mutation_scale=13,color='#ae742f',lw=1.4,zorder=5))
ax.text(24,21.7,'PRIMARY REPAIR ESCAPE LOOP',ha='center',fontsize=8.5,color='#9a6528',weight='bold')
ax.plot([3],[-2.4],marker='^',markersize=9,color='#347f61',zorder=11); ax.text(4.1,-2.6,'spawn',fontsize=8,color='#347f61')
ax.plot([7],[8],marker='o',markersize=7,color='#ba5340',zorder=11); ax.text(7.7,8,'Listener start',fontsize=8,color='#8a4235',va='center')
ax.annotate('',xy=(0,33),xytext=(36,33),arrowprops={'arrowstyle':'|-|','color':'#667b7c','lw':1}); ax.text(18,33.45,'36 m',ha='center',fontsize=10,color='#516469')
ax.annotate('',xy=(-1,-4),xytext=(-1,32),arrowprops={'arrowstyle':'|-|','color':'#667b7c','lw':1}); ax.text(-1.55,14,'36 m',rotation=90,ha='center',va='center',fontsize=10,color='#516469')
ax.plot([26,36],[-4.7,-4.7],color='#334c52',lw=2);ax.text(31,-5.15,'10 metres',ha='center',fontsize=8.5,color='#334c52')
ax.set_xlim(-2,37);ax.set_ylim(-5.5,34);ax.set_aspect('equal');ax.axis('off')
fig.text(.06,.018,'Blockout geometry only. Linked gate mesh clearance, NavMesh, Listener animation and headset scale remain pending Unity validation.',fontsize=8.7,color='#657777')
fig.savefig(OUT/'Level2_Floorplan_v04.png',dpi=170,facecolor=fig.get_facecolor())
plt.close(fig)
print(OUT/'Level2_Floorplan_v04.png')
