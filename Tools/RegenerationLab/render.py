#!/usr/bin/env python3
"""Render ACTUAL serialized Unity Mesh data and assigned material/texture assets in VTK.

These are modeling-tool previews, NOT Unity screenshots or target-device evidence.
No geometry is re-created from the authoring recipe in this renderer.
"""
from __future__ import annotations
import argparse
import json
import re
from pathlib import Path
import numpy as np
import vtk
from vtk.util.numpy_support import numpy_to_vtk,numpy_to_vtkIdTypeArray
from PIL import Image,ImageDraw,ImageFont
from validate import read_mesh,ROOT


def asset_map(root):
    result={}
    for meta in (root/ROOT).rglob('*.meta'):
        found=re.search(r'^guid: ([0-9a-f]{32})$',meta.read_text(),re.M)
        if found:result[found[1]]=meta.with_name(meta.name[:-5])
    return result


def actors(root, record, position=(0,0,0)):
    mapping=asset_map(root);result=[]
    for info in (record,record['label_mesh']):
        v,ids,subs,bounds=read_mesh(root/ROOT/'Meshes'/(info['name']+'.asset'))
        for (start,count),matname in zip(subs,info['material_slots']):
            poly=vtk.vtkPolyData();points=vtk.vtkPoints()
            points.SetData(numpy_to_vtk(np.ascontiguousarray(v[:,:3]),deep=True));poly.SetPoints(points)
            cells=vtk.vtkCellArray()
            offsets=np.arange(0,count+1,3,dtype=np.int64)
            cells.SetData(numpy_to_vtkIdTypeArray(offsets,deep=True),numpy_to_vtkIdTypeArray(ids[start:start+count].astype(np.int64),deep=True))
            poly.SetPolys(cells)
            poly.GetPointData().SetNormals(numpy_to_vtk(np.ascontiguousarray(v[:,3:6]),deep=True))
            poly.GetPointData().SetTCoords(numpy_to_vtk(np.ascontiguousarray(v[:,10:12]),deep=True))
            mapper=vtk.vtkPolyDataMapper();mapper.SetInputData(poly)
            actor=vtk.vtkActor();actor.SetMapper(mapper);actor.SetPosition(position)
            mat=(root/ROOT/'Materials'/(matname+'.mat')).read_text()
            tint=tuple(map(float,re.search(r'- _Color: \{r: (.*?), g: (.*?), b: (.*?), a:',mat).groups()))
            metallic=float(re.search(r'- _Metallic: (.+)',mat)[1]);smooth=float(re.search(r'- _Glossiness: (.+)',mat)[1])
            main=mat.split('    - _MainTex:')[1].split('    - _MetallicGlossMap:')[0]
            texture_path=mapping[re.search(r'guid: ([0-9a-f]{32})',main)[1]]
            reader=vtk.vtkPNGReader();reader.SetFileName(str(texture_path));reader.Update()
            tex=vtk.vtkTexture();tex.SetInputConnection(reader.GetOutputPort());tex.InterpolateOn();tex.MipmapOn()
            tex.SetUseSRGBColorSpace(True)
            if texture_path.stem=='PolymerLabels':tex.RepeatOff();tex.EdgeClampOn()
            else:tex.RepeatOn()
            prop=actor.GetProperty();prop.SetInterpolationToPBR();prop.SetColor(tint)
            prop.SetMetallic(metallic);prop.SetRoughness(1-smooth);prop.SetBaseColorTexture(tex)
            prop.BackfaceCullingOn();result.append(actor)
    return result


def environment():
    # Original broad studio lighting environment, not an imported HDRI.
    w,h=256,128
    y,x=np.mgrid[0:h,0:w]
    values=.16+.40*np.exp(-((y-90)/45)**2)
    values+=2.8*np.exp(-((x-65)/20)**2-((y-82)/14)**2)
    values+=1.5*np.exp(-((x-193)/24)**2-((y-77)/24)**2)
    rgb=np.repeat(values[:,:,None],3,axis=2).astype(np.float32)
    image=vtk.vtkImageData();image.SetDimensions(w,h,1)
    image.GetPointData().SetScalars(numpy_to_vtk(rgb.reshape(-1,3),deep=True))
    tex=vtk.vtkTexture();tex.SetInputData(image);tex.InterpolateOn();tex.MipmapOn()
    return tex


def render(root, records, path, rear=False):
    ren=vtk.vtkRenderer();ren.SetBackground(.072,.090,.093);ren.SetBackground2(.15,.178,.18);ren.GradientBackgroundOn()
    ren.UseImageBasedLightingOn();ren.SetEnvironmentTexture(environment())
    ren.SetAutomaticLightCreation(False)
    if len(records)==3:positions=[(-1.05,0,0),(0,0,.10),(1,0,0)]
    else:positions=[(0,0,0)]
    for record,pos in zip(records,positions):
        for actor in actors(root,record,pos):ren.AddActor(actor)
    plane=vtk.vtkPlaneSource();plane.SetOrigin(-25,-.002,-25);plane.SetPoint1(25,-.002,-25);plane.SetPoint2(-25,-.002,25)
    mapper=vtk.vtkPolyDataMapper();mapper.SetInputConnection(plane.GetOutputPort())
    floor=vtk.vtkActor();floor.SetMapper(mapper);floor.GetProperty().SetColor(.075,.093,.096)
    floor.GetProperty().SetInterpolationToPBR();floor.GetProperty().SetRoughness(.92);ren.AddActor(floor)
    for pos,intensity in [((-3,5,4),2.0),((4,3,1),.9),((0,4,-3),1.4)]:
        light=vtk.vtkLight();light.SetLightTypeToSceneLight();light.SetPosition(pos);light.SetFocalPoint(0,.8,0);light.SetIntensity(intensity);ren.AddLight(light)
    ren.UseFXAAOn()
    ssao=vtk.vtkSSAOPass();ssao.SetDelegatePass(vtk.vtkRenderStepsPass());ssao.SetRadius(.13);ssao.SetBias(.002);ssao.SetKernelSize(128);ssao.BlurOn();ren.SetPass(ssao)
    camera=ren.GetActiveCamera();camera.SetViewUp(0,1,0);camera.ParallelProjectionOn()
    if len(records)==3:
        camera.SetPosition((-3.5,2.7,-5.8) if rear else (3.3,2.7,5.8));camera.SetFocalPoint(0,.8,0);camera.SetParallelScale(1.30);size=(1800,1100)
    else:
        height=records[0]['size_xyz_m'][1]
        centre=height/2+.025
        camera.SetPosition((-2.5,centre+1.1,-4.1) if rear else (2.5,centre+1.1,4.1))
        camera.SetFocalPoint(0,centre,0);camera.SetParallelScale(height*.62+.15);size=(1000,1200)
    win=vtk.vtkRenderWindow();win.SetOffScreenRendering(1);win.SetMultiSamples(0);win.AddRenderer(ren);win.SetSize(size)
    ren.ResetCameraClippingRange();win.Render()
    grab=vtk.vtkWindowToImageFilter();grab.SetInput(win);grab.SetInputBufferTypeToRGB();grab.ReadFrontBufferOff();grab.Update()
    writer=vtk.vtkPNGWriter();writer.SetFileName(str(path.with_suffix('.raw.tmp.png')));writer.SetInputConnection(grab.GetOutputPort());writer.Write();win.Finalize()
    # Caption only. All prop pixels above it come from the serialized asset render.
    with Image.open(path.with_suffix('.raw.tmp.png')) as original:img=original.copy()
    draw=ImageDraw.Draw(img)
    try:
        large=ImageFont.truetype('DejaVuSans.ttf',30);small=ImageFont.truetype('DejaVuSans.ttf',19)
    except OSError:large=small=ImageFont.load_default()
    title='REGENERATION LAB / THREE-PIECE REVIEW KIT' if len(records)==3 else records[0]['name'].replace('RGL_','').replace('TreatmentTrolley','TREATMENT TROLLEY').replace('ChemicalDeliveryStand','CHEMICAL DELIVERY STAND').replace('SpecimenColdCabinet','SPECIMEN COLD CABINET')
    draw.text((34,27),title,font=large,fill=(219,228,219))
    draw.rectangle((0,size[1]-72,size[0],size[1]),fill=(20,29,30))
    draw.text((34,size[1]-53),'VTK modeling preview of baked assets | NOT Unity or headset evidence',font=small,fill=(189,205,202))
    temp=path.with_suffix('.caption.tmp.png')
    img.save(temp,optimize=True)
    temp.replace(path)
    path.with_suffix('.raw.tmp.png').unlink()


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--root',type=Path,required=True);parser.add_argument('--out',type=Path,required=True)
    args=parser.parse_args();args.out.mkdir(parents=True,exist_ok=True)
    records=json.loads((args.root/ROOT/'RegenerationLab.inventory.json').read_text())['props']
    render(args.root,records,args.out/'kit-front.png');render(args.root,records,args.out/'kit-rear.png',True)
    for record in records:render(args.root,[record],args.out/(record['name']+'.png'))
