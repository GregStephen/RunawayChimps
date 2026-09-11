"""Draw exact top-down geometry from layout.json; not concept-generated imagery."""
from pathlib import Path
import json, sys
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.patches import Rectangle, FancyArrowPatch
HERE=Path(__file__).resolve().parent
OUT=Path(sys.argv[1]);OUT.mkdir(parents=True,exist_ok=True)
d=json.loads((HERE/'layout.json').read_text());nodes=d['nodes'];byid={n['id']:n for n in nodes}
plt.rcParams['font.family']='DejaVu Sans'
fig,ax=plt.subplots(figsize=(12,12.6),dpi=170)
fig.patch.set_facecolor('#f4f3ef');ax.set_facecolor('#f4f3ef')
fig.subplots_adjust(left=.04,right=.97,top=.87,bottom=.055)
fig.text(.06,.962,'RUNAWAY CHIMPS',fontsize=12,weight='bold',color='#657777')
fig.text(.06,.925,'Level 2 · Behavioral Conditioning',fontsize=24,weight='bold',color='#1c343d')
fig.text(.06,.893,'Blockout v0.3  /  Optional reward room at the bottom right',fontsize=11,color='#516469')
palette={'SafeFloor':'#c1dbcf','Floor':'#e2dfd3','RepairFloor':'#efdbb9','BypassFloor':'#cbd9df'}
for name,x0,x1,z0,z1,mat in d['rooms']:
    ax.add_patch(Rectangle((x0,z0),x1-x0,z1-z0,facecolor='#b5dbe2' if name=='Optional_Reward_Room' else palette[mat],zorder=1))
    for x in range(int(x0)+1,int(x1)):ax.plot([x,x],[z0,z1],color='white',alpha=.35,lw=.6,zorder=2)
    for z in range(int(z0)+1,int(z1)):ax.plot([x0,x1],[z,z],color='white',alpha=.35,lw=.6,zorder=2)
for n in nodes:
    if not n['material']:continue
    parent=byid[n['parent']]['name'] if n['parent'] else ''
    x,y,z=n['position'];sx,sy,sz=n['scale']
    if parent.startswith('02_') and y-sy/2<1:
        ax.add_patch(Rectangle((x-sx/2,z-sz/2),sx,sz,fc='#2e444a',zorder=7))
    if parent.startswith('04_'):
        ax.add_patch(Rectangle((x-sx/2,z-sz/2),sx,sz,fc='#82989e',ec='#334d56',lw=1.5,zorder=5))
ax.add_patch(Rectangle((12.8,13.345),2.4,.55,fc='#b6752a',ec='#795226',lw=1.2,zorder=8))
ax.add_patch(Rectangle((1.825,9.91),2.35,.18,fc='#384e50',zorder=8))
ax.add_patch(Rectangle((14.9,-.09),2.2,.18,fc='#285d70',zorder=8))
ax.add_patch(Rectangle((17.3,.2),.5,.5,fc='#238aab',ec='#ffffff',zorder=8))
for x in [17.4,17.55,17.7]:ax.plot([x,x],[.3,.6],color='white',lw=1.3,zorder=9)
for a,b,z,c in [(1.4,4.6,0,'#398260'),(10.4,13.6,10,'#b37d32'),(14.4,17.6,10,'#b37d32')]:
    ax.plot([a,b],[z,z],color=c,lw=2,zorder=8)
    ax.plot([a,a],[z-.22,z+.22],color=c,lw=1,zorder=8);ax.plot([b,b],[z-.22,z+.22],color=c,lw=1,zorder=8)
pts=[(3,2),(3,8.5),(12,8.5),(12,11.7),(16,11.7),(16,2.2),(8.4,1.9),(3,2)]
ax.plot(*zip(*pts),ls=(0,(5,4)),color='#ae742f',lw=1.7,zorder=4)
for a,b in [((5,8.5),(6,8.5)),((14,11.7),(15,11.7)),((16,6),(16,5)),((8,1.95),(7,1.94))]:
    ax.add_patch(FancyArrowPatch(a,b,arrowstyle='-|>',mutation_scale=13,color='#ae742f',lw=1.5,zorder=5))
def txt(x,z,text,size=12,weight='normal',color='#213c45',**kw):
    ax.text(x,z,text,ha='center',va='center',fontsize=size,fontweight=weight,color=color,zorder=10,**kw)
txt(3,-1.3,'SAFE ENTRY',15,'bold');txt(3,-2.15,'6 × 4 m',11)
txt(3,-3.05,'Arrival + return-control marker',8.5)
txt(3,12.8,'EXIT',15,'bold');txt(3,12.1,'6 × 4 m',11)
txt(3,11.25,'Personal completion required',8.2)
txt(8.8,12.7,'02',34,'bold',color='#b5bfba');txt(8.3,11.8,'TEST WING',8.5,'bold',color='#758884')
txt(16,-1.05,'LOCKED\nREWARD ROOM',10.5,'bold')
txt(16,-2.15,'4 × 4 m',10)
txt(16,-3.1,'Collectible / currency',7.5)
txt(16,1.1,'Small door + scanner',8.2)
txt(14,14.7,'REPAIR ROOM · 8 × 4 m',13,'bold')
txt(14,12.55,'Noisy release mechanism',9.5)
txt(16,7.1,'BYPASS',11,'bold');txt(16,6.5,'4 m nominal',9)
txt(7.3,9.45,'TEST HALL · 14 × 10 m',12,'bold')
txt(6.1,5.5,'SOLID\nOBSTACLE A',9,'bold')
txt(10.2,5.1,'SOLID\nWALL\nB',8,'bold')
txt(12,10.43,'3.2 m',8.5);txt(16,10.43,'3.2 m',8.5)
txt(3,9.48,'EXIT GATE',8,'bold')
ax.plot([3],[ -2.6],marker='^',markersize=9,color='#347f61',zorder=11)
ax.plot([3],[6.6],marker='o',markersize=8,color='#ba5340',zorder=10)
txt(1.55,6.6,'Listener\nspawn',8)
ax.plot([.15,.7],[ -2,-2],lw=8,color='#398260',zorder=8)
# Overall nominal envelope and scale, matching the original sketch.
ax.annotate('',xy=(0,15.6),xytext=(18,15.6),arrowprops={'arrowstyle':'|-|','color':'#667b7c','lw':1})
txt(9,16.03,'18 m overall',10,color='#516469')
ax.annotate('',xy=(-.9,-4),xytext=(-.9,14),arrowprops={'arrowstyle':'|-|','color':'#667b7c','lw':1})
ax.text(-1.35,5,'18 m overall',rotation=90,ha='center',va='center',fontsize=10,color='#516469')
ax.text(7,-.9,'BLOCKOUT NOTES',fontsize=11,weight='bold',color='#233f48')
notes=['Card comes from another level.',
       'Matching color + symbol identifies it.',
       'Level 4 is an example; source is open.',
       'Dashed line: possible patrol route.',
       'Doors, scanner and rewards are unwired.']
for i,t in enumerate(notes):ax.text(7,-1.5-i*.51,t,fontsize=8.5,color='#435b60')
ax.plot([12,17],[-4.38,-4.38],color='#334c52',lw=2)
for x in [12,13,14,15,16,17]:ax.plot([x,x],[-4.45,-4.3],color='#334c52',lw=1)
txt(14.5,-4.8,'5 metres',8.5)
ax.set_xlim(-1.8,19);ax.set_ylim(-5.2,16.5);ax.set_aspect('equal');ax.axis('off')
fig.text(.06,.022,'Door symbols are schematic. Unity reuses the existing gate prefabs; mesh clearance and headset checks are pending.',fontsize=9,color='#657777')
fig.savefig(OUT/'Level2_Floorplan.png',dpi=170,facecolor=fig.get_facecolor())
plt.close(fig)
print('Floorplan drawn from layout.json')
