using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Security;
using System.IO;

namespace bboard {
    public partial class fMain : Form {
        const int ZoomBase=120,MinZoom=8,MaxZoom=1960;
        int NewWidth,NewHeight;
        Brush BackBrush=Brushes.LightGray;
        bool Dirty=false;
        OpenFileDialog ofd=new OpenFileDialog();
        SaveFileDialog sfd,efd;
        PrintDialog pd;PageSetupDialog paged;
        hfmap map,copymap;
        List<UndoItem> undos=new List<UndoItem>();
        int undoc;string undop;
        const int UndoMax=5;
        byte[] draw,orig;
        Bitmap bm;
        Combine Comb=Combine.Max;
        bezier BezUser=new bezier(new double[] {0,0,0.25,0 ,0.25,0.5,0.5,0.5,0.75,0.5 ,0.75,1,1,1});
        bezier BezCylinder=new bezier(new double[] {0,0,0.2,0.8,0.2,0.8,1,1}); //{0,1,0.25,1,0.75,1,1,1});
        bezier BezCone=new bezier(new double[] {0,0,0.25,0.25,0.75,0.75,1,1});
        bezier BezSphere=new bezier(new double[] {0,0,0,0.5,0.5,1,1,1});
        bezier BezNail=new bezier(new double[] {0,0,0.5,0.2,.8,0.5,1,1});
        bezier BezSigma=new bezier(new double[] {0,0,0.25,0,.75,1,1,1});
        bezier Bez;
        double[] shape=new double[1024];
        bool White=true,AutoSnap=false;
        ColorDialog CDialog=new ColorDialog();
        bool timeDraw=false;
        int NoDraw=0;
        int mseq=0; // mouse sequence
        int lmx,lmy; // last mouse position
        int pmk,pix,piy; // press mouse position and keys
        MouseButtons pmb;
        int Color2=Pal.White,Color3=Pal.Black;
        int sx=0,sy=0,zoom=ZoomBase,angle=0;
        double cos=1,sin=0;
        byte Height2=255;
				fHelp Help;
        int Shape=0,Radius=15;
        
        int IX(int x,int y) { return (int)(((x-sx)*cos+(y-sy)*sin)*ZoomBase/zoom);}
        int IY(int x,int y) { return (int)((-(x-sx)*sin+(y-sy)*cos)*ZoomBase/zoom);}
        void S2I(int x,int y,out int ix,out int iy) {
          ix=IX(x,y);iy=IY(x,y);
        }
        int SX(int x,int y) { double rx=x*zoom/ZoomBase,ry=y*zoom/ZoomBase; return sx+(int)(rx*cos-ry*sin);}
        int SY(int x,int y) { double rx=x*zoom/ZoomBase,ry=y*zoom/ZoomBase; return sy+(int)(+rx*sin+ry*cos);}
        
        void NewSize(string par) {
          string[] sa=par.Split('x','X',',');
          int.TryParse(sa[0],out NewWidth);
          if(sa.Length>1) int.TryParse(sa[1],out NewHeight);else NewHeight=NewWidth;
        }

        public fMain(string[] arg) {
            //for(int y=0;y<map.Data.Length;y++) map.Data[y]=(float)(y/512/512.0);
            //map.FuncRadial(Bez,Combine.Max,.5,.5,.3,1);           
            //map.FloodFill(0.9,0.9,0.33);
            InitializeComponent();
            br3.BackColor=bh5.BackColor=Color.DarkGray;
            int a=0;
            while(a<arg.Length&&arg[a].Length>0&&arg[a][0]=='-') {
              string opt=arg[a++],opt2="";
              if(opt.Length<2) break;
              if(opt.Length>2) opt2=opt.Substring(2);
              switch(opt[1]) {
               case 'n':if(opt2=="") opt2=arg[a++];NewSize(opt2);break;               
              }
            }
            string fname=a<arg.Length?arg[a]:null;
            Bez=BezCone;Bez.Shape(shape);            
            if(fname==null) {
              NewFile();
              UpdateBitmap();
              Center();
              SetWhite(true,true,false);
            } else
              LoadFile(fname,true);
            SetColor(White?0:Pal.White);
        }
        public void CheckBitmap() {
          if(bm==null||bm.Width!=map.Width||bm.Height!=map.Height) UpdateBitmap();
        }
        public void UpdateBitmap() {
          if(bm!=null) bm.Dispose();
          bm=new Bitmap(map.Width,map.Height,PixelFormat.Format32bppRgb);//PixelFormat.Format24bppRgb);
          draw=new byte[map.Width*map.Height];
          orig=null;
        }
        public void Repaint(bool dirty) {
          if(NoDraw<1)
            Repaint(0,0,map.Width,map.Height,dirty);
        }
        int Clip(int x,int max) {
          return x<0?0:x>max?max:x;
        }
				void HelpCmd() {
				  if(Help==null) {
            string file=GetType().Assembly.Location;
            file=file.Substring(0,file.Length-3)+"rtf";
            if(!File.Exists(file)) return;
            Help=new fHelp(file);
          }  
					Help.ShowDialog(this);
				}
        public void Repaint(int x0,int y0,int x1,int y1,int e,bool dirty) {
          int x;
          if(x0>x1) {x=x0;x0=x1;x1=x;}
          if(y0>y1) {x=y0;y0=y1;y1=x;}
          Repaint(x0-e,y0-e,x1+e,y1+e,dirty);
        }
        public void Xor(int x0,int y0,int x1,int y1,int color) {
          BitmapData bd=bm.LockBits(new Rectangle(x0,y0,x1-x0+1,y1-y0+1),ImageLockMode.ReadWrite,bm.PixelFormat);
          int s=bd.Stride/4,w=x1-x0+1;
          unsafe { fixed(byte* md=map.Data) {
            int* p=(int*)bd.Scan0.ToPointer();
            for(int y=y0;y<=y1;y++) {
              for(int* q=p,qe=q+w;q<qe;q++)
                *q^=color;
              p+=s;
            }            
          }}
          bm.UnlockBits(bd);
        }
        public void Repaint(int x0,int y0,int x1,int y1,bool dirty) {
          x0=Clip(x0,map.Width);x1=Clip(x1,map.Width);
          y0=Clip(y0,map.Height);y1=Clip(y1,map.Height);
          int w=map.Width,h=map.Height;
          //int bpl=(w*3+3)&~3;         
          int i=0;
          //w*=3;
          //int color=0xff00ff;
         if(dirty) {
          BitmapData bd=bm.LockBits(new Rectangle(0,0,map.Width,map.Height),ImageLockMode.WriteOnly,bm.PixelFormat);          
          //Marshal.Copy(bytes,0,bd.Scan0,bytes.Length);
          int ye=bd.Stride*Height;
          unsafe { fixed(byte* md=map.Data,dd=draw,od=orig) {
            byte* p=(byte*)bd.Scan0.ToPointer();
            p+=bd.Stride*y0+4*x0;
  
           i=4*(y0*map.Width+x0);
           int rest=4*(map.Width-(x1-x0));
           int restp=bd.Stride-4*(x1-x0);
           for(int y=y0;y<y1;y++) {
             for(int x=x0;x<x1;x++) {
               int color=md[i]|(md[i+1]<<8)|(md[i+2]<<16);
               byte di2=dd[i>>2];
               byte di=md[i+3];
               i+=4;
               if(di2>0) {
                 if(Comb==Combine.None) {
                   if(White) color=hfmap.Mix(di2,Color2,128,color);
                   color=hfmap.Mix(di2,Color2,224,color);
                 } else {
                   color=hfmap.Mix(di2,Color2,di,color);
                   if(Comb==Combine.Max) {
                     if(di2>di)
                       di=di2;
                   } else if(Comb==Combine.Min) {
                     di2=(byte)(255-di2);
                     if(di2<di)
                       di=di2;
                   } else if(Comb==Combine.Mul) {
                     if(di<255) di+=(byte)(((255-di)*di2)/255/2);
                   } else if(Comb==Combine.Div) {
                     if(di>0) di-=(byte)((di*di2)/255/2); 
                   } else if(Comb==Combine.Add) {
                     int r=di+di2/4;
                     di=(byte)(r<255?r:255);
                   } else if(Comb==Combine.Sub) {
                     int r=di-di2/4;
                     di=(byte)(r>0?r:0);
                   }
                 }
               }
               if(White) {
                 *p++=(byte)(255-(di*(255-((color>>0)&255)))/255);
                 *p++=(byte)(255-(di*(255-((color>>8)&255)))/255);
                 *p++=(byte)(255-(di*(255-((color>>16)&255)))/255);
               } else {
                 *p++=(byte)((di*((color>>0)&255))/255);
                 *p++=(byte)((di*((color>>8)&255))/255);
                 *p++=(byte)((di*((color>>16)&255))/255);
               }
               p++;
               if(od!=null) {
                 byte *o=od+i;
                 p[-4]=(byte)((p[-4]*3+o[0])/4);
                 p[-3]=(byte)((p[-3]*3+o[1])/4);
                 p[-2]=(byte)((p[-2]*3+o[2])/4);
               }
               //Palette.Color(bytes,y+x,map.Data[i++]/255.0,palette,true);
             }
             p+=restp;
             i+=rest;
           }
          }}
          bm.UnlockBits(bd);
         }
          Graphics gr=this.CreateGraphics();
          if(angle>0) {
            float z=zoom*1f/ZoomBase;
            gr.TranslateTransform(sx,sy);
            gr.RotateTransform(angle);
            gr.ScaleTransform(z,z);
            gr.DrawImage(bm,new Rectangle(x0,y0,x1-x0,y1-y0),x0,y0,x1-x0,y1-y0,GraphicsUnit.Pixel);
            if(x0==0&&y0==0&&x1==map.Width&&y1==map.Height) {
              gr.FillRectangle(BackBrush,-2*Width,-2*Height,2*Width,4*Height+map.Height);
              gr.FillRectangle(BackBrush,map.Width,-2*Height,2*Width,4*Height+map.Height);
              gr.FillRectangle(BackBrush,0,-2*Height,map.Width,2*Height);
              gr.FillRectangle(BackBrush,0,map.Height,map.Width,2*Height);
            }
          } else {
            //gr.DrawImageUnscaled(bm,0,0);
            //int sy=this.MainMenuStrip.Height;
            gr.DrawImage(bm,new Rectangle(SX(x0,y0),SY(x0,y0),zoom*(x1-x0)/ZoomBase,zoom*(y1-y0)/ZoomBase),x0,y0,x1-x0,y1-y0,GraphicsUnit.Pixel);          
            if(x0==0&&y0==0&&x1==map.Width&&y1==map.Height) {
              if(sy>0) gr.FillRectangle(BackBrush,0,0,Width,sy);
              int ey=sy+zoom*map.Height/ZoomBase;
              if(ey<Height) gr.FillRectangle(BackBrush,0,ey,Width,Height-ey);
              if(sx>0) gr.FillRectangle(BackBrush,0,sy,sx,ey-sy);
              int ex=sx+zoom*map.Width/ZoomBase;
              if(ex<Width) gr.FillRectangle(BackBrush,ex,sy,Width-ex,ey-sy);
            
            }
          }
          gr.Dispose();
        }
        void SetRadius(int x) {
          if(x<1)  x=1;else if(x>99) x=99;
          Radius=x;
          string s=x.ToString("00");
          bRadius1.Text=""+s[1];
          bRadius2.Text=""+s[0];
          br1.BackColor=br2.BackColor=br3.BackColor=br4.BackColor=br5.BackColor=SystemColors.Control;
          Button b=null;
          if(x==5) b=br1;
          else if(x==10) b=br2;
          else if(x==15) b=br3;
          else if(x==20) b=br4;
          else if(x==25) b=br5;
          if(b!=null) b.BackColor=Color.DarkGray;
        }
        void UpdateSin() {
          cos=Math.Cos(angle*Math.PI/180);sin=Math.Sin(angle*Math.PI/180);
        }
        void SetAngle(int x,int y,int a) {
          a%=360;
          if(a<0) a+=360;
          int ox=IX(x,y),oy=IY(x,y),nx;
          angle=a;
          UpdateSin();
          sx=0;sy=0;
          nx=x-SX(ox,oy);sy=y-SY(ox,oy);sx=nx;
        }

        protected override void OnMouseWheel(MouseEventArgs e) {
          int x=IX(e.X,e.Y),y=IY(e.X,e.Y),nx;
          int d=e.Delta;
          bool shift=GDI.ShiftKey,ctrl=GDI.CtrlKey;
          if(shift&&ctrl) {
            SetAngle(e.X,e.Y,angle+(-d/120*15));
            timeDraw=true;
            return;
          }
          if(shift|ctrl) {
            if(shift) sx+=d;
            else sy+=d;
            timeDraw=true;
            return;
          }           
          if(d<0) 
            while(d<0&&zoom>=MinZoom) {
              zoom=zoom*3/4;
              d+=120;
            }
          else 
            while(d>0&&zoom<MaxZoom) {
              zoom=zoom*4/3;
              d-=120;
            }  
          sx=0;sy=0;          
          nx=e.X-SX(x,y);sy=e.Y-SY(x,y);sx=nx;
          timeDraw=true;
        }
        void Fullscreen() {
          bool f=FormBorderStyle!=FormBorderStyle.None;
          if(!f&&(zoom!=ZoomBase||angle!=0)) {
            Center();
            Repaint(false);
            return;
          }
          NoDraw++;
          //MainMenuStrip.Visible=!f;
          FormBorderStyle=f?FormBorderStyle.None:FormBorderStyle.Sizable;
          WindowState=f?FormWindowState.Maximized:FormWindowState.Normal;          
          Center();
          NoDraw--;
        }
        void Center() {
          sin=angle=0;cos=1;
          sx=sy=0;zoom=ZoomBase;
          if(map!=null) {
            sx=SX(map.Width,0);sy=SY(0,map.Height);
            sx=(Width-sx)/2;sy=(Height-sy)/2;
          }
        }
        void Clear(bool conly) {
          PushUndo(true);
          map.Clear(0,White?0:Pal.White,conly);
          Center();
          Repaint(true);
          if(conly) SetCombine(0);
          else if(miCombine.Image==miCombineNone.Image) SetCombine(1);
        }
        void Rotate90(bool counter) {
          if(pmb==MouseButtons.None) PushUndo(false);
          map.Rotate90(counter,draw);
          map.Rotate90x4(counter,orig);
          map=map.Rotate90(counter);
          bm.RotateFlip(counter?RotateFlipType.Rotate90FlipNone:RotateFlipType.Rotate270FlipNone);
          
          
          //UpdateBitmap();
          

          int ix=IX(lmx,lmy),iy=IY(lmx,lmy),yx=ix,yy=iy;
          if(ix>=0&&iy>=0&&ix<map.Height-2&&iy<map.Width-2) {            
            if(counter) {yx=iy;yy=map.Height-2-ix;}
            else {yx=map.Width-2-iy;yy=ix;}
          } else {
            if(ix>map.Height/2-1) yx-=map.Height-map.Width;
            if(iy>map.Width/2-1) yy-=map.Width-map.Height;
          } 
          int nx=SX(yx,yy),ny=SY(yx,yy);
          sx-=nx-lmx;sy-=ny-lmy;
                    
          int px=pix,py=piy;
          if(counter) {pix=py;piy=map.Height-2-px;}
          else {pix=map.Width-2-py;piy=px;}
          
          Repaint(true);
        }
        void Mirror(bool vertical) {
          PushUndo(false);
          map.Mirror(vertical,draw);
          map.Mirror(vertical);
          if(pmb!=MouseButtons.None) {            
            if(vertical) {
              int iy=IY(lmx,lmy),y=map.Height-1-iy,d=y-iy;
              sy-=d*zoom/ZoomBase;
              piy=map.Height-1-piy;
            } else {
              int ix=IX(lmx,lmy),x=map.Width-1-ix,d=x-ix;
              sx-=d*zoom/ZoomBase;
              pix=map.Width-1-pix;
            }
          }
          Repaint(true);
        }
        void AutoShrink() {
          int x0=0,y0=0,x1=map.Width,y1=map.Height;
          if(map.Bounding(ref x0,ref y0,ref x1,ref y1))
            Shrink(x0,y0,x1,y1);
        }
        void Shrink(int x0,int y0,int x1,int y1) {
          if(!map.Intersected(ref x0,ref y0,ref x1,ref y1)||(x0==0&&y0==0&&x1==map.Width-1&&y1==map.Height-1)) return;          
          PushUndo(false);
          hfmap m2=new hfmap(x1-x0+1,y1-y0+1);
          m2.Copy(0,0,map,x0,y0,x1,y1);
          map=m2;
          CheckBitmap();
          Repaint(true);
        }
        void Extent(int x,int y) {
          hfmap m2=map.Extent(x,y);
          if(m2==null) return;
          PushUndo(false);
          map=m2;
          UpdateBitmap();
          int dx=0,dy=0;
          if(x<0) dx=-x;
          if(y<0) dy=-y;
          sx-=(int)((dx*cos-dy*sin)*zoom/ZoomBase);
          sy-=(int)((+dx*sin+dy*cos)*zoom/ZoomBase);
          Repaint(true);
          
        }
        void Duplicate(int x0,int y0,int x1,int y1,bool vertical) {
           bool nx=x1<x0,ny=y1<y0;
           int dx=vertical?nx?x1:x0:nx?2*x1-x0+1:x1+1,dy=vertical?ny?2*y1-y0+1:y1+1:ny?y1:y0;
           PushUndo(true);
           map.Copy(dx,dy,map,x0,y0,x1,y1);
           Repaint(true);
           if(vertical) piy=y1+(ny?-1:1);
           else pix=x1+(nx?-1:1);
        }
        void Copy(int x0,int y0,int x1,int y1) {
           if(!map.Intersected(ref x0,ref y0,ref x1,ref y1)) return;
           int xorc=0x808080;
           Xor(x0,y0,x1,y1,xorc);
           Repaint(x0,y0,x1,y1,false);             
           copymap=new hfmap(x1-x0+1,y1-y0+1);
           copymap.Copy(0,0,map,x0,y0,x1,y1);
           System.Threading.Thread.Sleep(250);
           Xor(x0,y0,x1,y1,xorc);
           Repaint(x0,y0,x1,y1,false);
        }
        Bitmap GetClipboard(string etext) {
         try {
          return Clipboard.GetImage() as Bitmap;
         } catch(Exception ex) {
           MessageBox.Show(this,ex.Message,etext+""==""?"Get clipboard":etext);
           return null;
         }
        }
        void PasteOrig(bool aspect) {
          if(orig!=null) {
            orig=null;
            goto rep;
          }
          Bitmap cb=GetClipboard("Paste");
          if(cb==null) return;
          int mw=map.Width,mh=map.Height;
          Bitmap ob=new Bitmap(mw,mh);
          int w2=mw,h2=mh;
          if(aspect) {
            if(w2*cb.Height>h2*cb.Width) 
              w2=(mh*cb.Width+cb.Height-1)/cb.Height;
            else
              h2=(mw*cb.Height+cb.Width-1)/cb.Width;
          }
          using(var gr=Graphics.FromImage(ob)) {            
            gr.DrawImage(cb,(mw-w2)/2,(mh-h2)/2,w2,h2);
          }
          cb.Dispose();
          BitmapData bd=ob.LockBits(new Rectangle(0,0,mw,map.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppRgb);
          orig=new byte[4*mw*map.Height];
          unsafe{ fixed(byte *od=orig) {
            int y;
            for(y=0;y<map.Height;y++) {
              byte* h=(byte*)bd.Scan0.ToPointer()+y*bd.Stride,g,ge;
              for(g=od+y*4*mw,ge=g+4*mw;g<ge;g++,h++)
                *g=*h;
            }
          }}
          ob.UnlockBits(bd);
         rep:
          Repaint(true);
        }        
        void Paste(int x0,int y0,int cx,int cy,bool min,bool clip) {
          int x1=map.Width,y1=map.Height,sx=0,sy=0,x,y;
          if(clip) {
            Bitmap cb=GetClipboard("Paste");
            if(cb==null) return;
           using(cb) {
            x0-=(cx<0?0:cx>0?2:1)*cb.Width/2;
            y0-=(cy<0?0:cy>0?2:1)*cb.Height/2;
             if(!map.Intersected(ref x0,ref y0,ref x1,ref y1,ref sx,ref sy,cb.Width,cb.Height)) return;
             BitmapData bd=cb.LockBits(new Rectangle(sx,sy,x1-x0+1,y1-y0+1),ImageLockMode.ReadOnly,PixelFormat.Format24bppRgb);
            unsafe{ fixed(byte* dd=draw) {
              byte *gd=(byte*)bd.Scan0.ToPointer();
              for(y=y0;y<=y1;y++) {
                byte* g=dd+map.Width*y+x0,h=gd+bd.Stride*(y-y0);
                for(x=x0;x<=x1;x++,g++,h+=3) {
                  int gv=(h[0]+h[1]+h[2]+2)/3;
                  if(White) gv=255-gv;
                  *g=(byte)gv;
                }
              }
            }}
             cb.UnlockBits(bd);
           }
          } else {
            if(copymap==null) return;
            //copymap.Clear(0,Color2,true);
            x0-=(cx<0?0:cx>0?2:1)*copymap.Width/2;
            y0-=(cy<0?0:cy>0?2:1)*copymap.Height/2;
            if(!map.Intersected(ref x0,ref y0,ref x1,ref y1,ref sx,ref sy,copymap.Width,copymap.Height)) return;
            unsafe{ fixed(byte* cd=copymap.Data,dd=draw) {
             for(y=y0;y<=y1;y++) {
               byte* g=dd+map.Width*y+x0,h=cd+(copymap.Width*(sy+y-y0)+sx)*4+3;
               for(x=x0;x<=x1;x++,g++,h+=4)
                 *g=*h;
             }
            }}
          }
          PushUndo(true);
          map.ApplyDraw(Color2,Height2,SubComb(Comb,min),draw,true,White,0);
          //map.Copy(x,y,copymap,0,0,copymap.Width-1,copymap.Height-1,min?'i':'a');
          Repaint(x0,y0,x1,y1,true);
        }
        void NoScale() {
          sx=sy=0;zoom=ZoomBase;
          Center();
          Repaint(false);
        }
        Bitmap BMResized() {
          var bm2=new Bitmap(map.Width*zoom/ZoomBase,map.Height*zoom/ZoomBase);
          using(Graphics g=Graphics.FromImage(bm2)) {
            g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.Bilinear;
            g.DrawImage(bm,0,0,bm2.Width,bm2.Height);
          }
          return bm2;
        }
        static double Sqrt(double x,double y) {
          return Math.Sqrt(x*x+y*y);
        }
        static int Sgn(double x) { return x<0?-1:x>0?1:0;}
        static int Sgn(double x,int y) { return Sgn(x)*(y<0?-y:y);}
        static double Sgn(double x,double y) { return Sgn(x)*(y<0?-y:y);}
        static double Sigma(double x) { return x<=0.5?x<=0?0:2*x*x:x>=1?1:1-2*(1-x)*(1-x);}
        static double Sigmacos(double x) { return x<=0?0:x>=1?1:(1-Math.Cos(x*Math.PI))/2;}
        static double Sigma2(double x) { return x<=0?0:x>=1?1:x<0.5?(1-Math.Sqrt(1-(+2*x)*(+2*x)))/2:1-(1-Math.Sqrt(1-4*(1-x)*(1-x)))/2;}

        void Funcx(double x,double y,double r,int h,byte[] draw,double[] shape) {
          if(Shape==1) map.FuncSquare(x,y,r,Height2,draw,shape);
          else if(Shape==2) map.FuncDiamond(x,y,r,Height2,draw,shape);
          else map.FuncRadial(x,y,r,Height2,draw,shape);
        }
        void Arc(int cx,int cy,int r,int sa,int da,int dr) {
          ArcD(cx,cy,r,sa*Math.PI/2,da*Math.PI/2,dr);
        }
        void ArcD(int cx,int cy,int r,double sa2,double da2,int dr) {
          double ra;
          for(int a=0,n=1+r;a<=n;a++) {
            ra=sa2+da2*a/n;            
            Funcx(cx+r*Math.Cos(ra),cy+r*Math.Sin(ra),dr,Height2,draw,shape);
          }
        }
        void ShapeU(bool shift,bool move,int mx,int my) {
          int dx=mx-pix,dy=my-piy,dr=Radius,nx=pix,ny=piy,cx=pix,cy=piy,r;
          int sa=-1;
          bool hori=Math.Abs(dx)>Math.Abs(dy);
          if(hori) {
            int ey=Math.Abs(dy),ex=Math.Abs(dx);
            cx=pix+Sgn(dx)*ey;r=ey;
            map.FuncLine(cx,my,mx,my,dr,Height2,draw,shape);
            sa=mx>pix?my>piy?1:2:my>piy?4:3;
          } else {
            int ex=Math.Abs(dx),ey=Math.Abs(dy);
            cy=piy+Sgn(dy)*ex;r=ex;
            map.FuncLine(mx,cy,mx,my,dr,Height2,draw,shape);
            sa=mx>pix?my>piy?3:4:my>piy?2:1;
          }
          if(shift) {           
            if(hori) {
               Arc(pix,piy+Sgn(dy,r/2),r/2,mx>pix?my>piy?3:0:my>piy?2:1,1,dr);
               Arc(pix+Sgn(dx,r),my-Sgn(dy,r/2),r/2,sa,1,dr);
            } else {
               Arc(pix+Sgn(dx,r/2),piy,r/2,mx>pix?my>piy?1:2:my>piy?0:3,1,dr);
               Arc(mx-Sgn(dx,r/2),piy+Sgn(dy,r),r/2,sa,1,dr);
            }
          } else
            Arc(cx,cy,r,sa,1,dr);
          Repaint(pix,piy,mx,my,dr,true);
          if(move) {pix=mx;piy=my;}
        }
        void ShapeR(bool move,int mx,int my) {
          int dr=Radius,dx,dy,x0=pix,y0=piy,x1,y1,x2=mx,y2=my,r=dr,x3,y3,x;
          double d=cos*(x2-x0)-sin*(y2-y0),e=cos*(y2-y0)+sin*(x2-x0),sa,r2;
          r2=Math.Abs(d);
          if(2*r>r2) r=(int)(r2/2);
          r2=Math.Abs(e);
          if(2*r>r2) r=(int)(r2/2);
          dx=(int)(d*cos);dy=(int)(-d*sin);
          x1=x0+dx;y1=y0+dy;
          x3=x2-dx;y3=y2-dy;
          if(d<0) {
            x=x1;x1=x0;x0=x;x=y1;y1=y0;y0=x;
            x=x3;x3=x2;x2=x;x=y3;y3=y2;y2=x;
          }
          if(e<0) {
            x=x3;x3=x0;x0=x;x=y3;y3=y0;y0=x;
            x=x1;x1=x2;x2=x;x=y1;y1=y2;y2=x;
          }
          double c2=(cos+sin)/Math.Sqrt(2),s2=(cos-sin)/Math.Sqrt(2);
          sa=Math.Atan2(s2,c2)+3*Math.PI/4;
          ArcD((int)(x0+r*c2),(int)(y0+r*s2),r,sa,Math.PI/2,dr);
          ArcD((int)(x1-r*s2),(int)(y1+r*c2),r,sa+Math.PI/2,Math.PI/2,dr);
          ArcD((int)(x2-r*c2),(int)(y2-r*s2),r,sa+Math.PI,Math.PI/2,dr);
          ArcD((int)(x3+r*s2),(int)(y3-r*c2),r,sa-Math.PI/2,Math.PI/2,dr);
          map.FuncLine((int)(x0+r*(c2-sin)),(int)(y0+r*(s2-cos)),(int)(x1-r*(s2+sin)),(int)(y1+r*(c2-cos)),dr,Height2,draw,shape);
          map.FuncLine((int)(x3+r*(s2+sin)),(int)(y3-r*(c2-cos)),(int)(x2-r*(c2-sin)),(int)(y2-r*(s2-cos)),dr,Height2,draw,shape);
          map.FuncLine((int)(x0+r*(c2-cos)),(int)(y0+r*(s2+sin)),(int)(x3+r*(s2-cos)),(int)(y3-r*(c2-sin)),dr,Height2,draw,shape);
          map.FuncLine((int)(x1-r*(s2-cos)),(int)(y1+r*(c2-sin)),(int)(x2-r*(c2-cos)),(int)(y2-r*(s2+sin)),dr,Height2,draw,shape);
          hfmap.MinMax(ref x1,ref y1,ref x3,ref y3,x0,y0,x2,y2);
          Repaint(x1,y1,x3,y3,2*dr,true);
          if(move) {pix=mx;piy=my;}
        }
        void ShapeP(bool quart,bool inv,bool move,int mx,int my) {
          int x=pix,y=piy,s;
          if(move) {pix=mx;piy=my;}
          if(inv) {s=x;x=mx;mx=s;s=y;y=my;my=s;}
          int dx=mx-x,dy=my-y,dr=Radius;
          double sa=-1,da,ra,r=-1,cx=x+mx,cy=y+my;
          if(quart) {
            cx=(cx+dy)/2;cy=(cy-dx)/2;
            da=Math.PI/2;
          } else {
            cx/=2;cy/=2;
              da=Math.PI;
          }                  
          sa=Math.Atan2(my-cy,mx-cx);
          r=Sqrt(mx-cx,my-cy);
          for(int a=0,n=1+Math.Abs(dx)+Math.Abs(dy);a<=n;a++) {
            ra=sa+da*a/n;
            Funcx(cx+r*Math.Cos(ra),cy+r*Math.Sin(ra),dr,Height2,draw,shape);
          }
          Repaint((int)(cx-r-dr),(int)(cy-r-dr),(int)(cx+r+dr),(int)(cy+r+dr),true);          
        }
        void Circle(double cx,double cy,double r) {
          int dr=Radius;
          for(int a=0,n=(int)(1+8*r);a<n;a++) {
            double ra=a*2*Math.PI/n;
            Funcx(cx+r*Math.Cos(ra),cy+r*Math.Sin(ra),dr,Height2,draw,shape);
          }
        }
        void ShapeO(bool shift,bool move,int mx,int my) {
          float cx,cy,rx,ry,r;
          if(shift) {
            cx=pix;cy=piy;rx=pix-mx;ry=piy-my;r=(float)(Math.Sqrt(rx*rx+ry*ry));
          } else {
            cx=(mx+pix)/2;cy=(my+piy)/2;rx=pix-mx;ry=piy-my;r=(float)(Math.Sqrt(rx*rx+ry*ry)/2);
            if(move) {pix=mx;piy=my;}
          }
          Circle(cx,cy,r);
          int dr=Radius;
          Repaint((int)(cx-r-dr),(int)(cy-r-dr),(int)(cx+r+dr),(int)(cy+r+dr),true);
        }
        void ShapeQ(bool shift,bool move,int mx,int my) {
          if(mx==pix&&my==piy) return;
          float cx,cy,dx,dy,e=shift?1/3f:2/3f;
          cx=(pix+mx)/2f;cy=(piy+my)/2f;dx=mx-cx;dy=my-cy;
          if(move) {pix=mx;piy=my;}
          int dr=Radius;
          for(int a=0,n=(int)(1+8*Math.Max(Math.Abs(dx),Math.Abs(dy)));a<n;a++) {
            double ra=a*2*Math.PI/n;
            Funcx(cx+dx*Math.Cos(ra)-e*dy*Math.Sin(ra),cy+e*dx*Math.Sin(ra)+dy*Math.Cos(ra),dr,Height2,draw,shape);
          }
          dx=Math.Abs(dx);dy=Math.Abs(dy);          
          Repaint((int)(cx-dx-dy-dr),(int)(cy-dx-dy-dr),(int)(cx+dx+dy+dr),(int)(cy+dx+dy+dr),true);
        }
        void ShapeG(bool shift,int mx,int my) {
          int px=pix,py=piy;
          hfmap.Sort(ref px,ref py,ref mx,ref my);
          int dx=mx-px,dy=my-py,m=dx<dy?dx:dy,r=shift?m/2:m*2/10,dr=Radius;
          for(int a=0,n=(int)(1+8*r);a<n;a++) {
            double ra=a*2*Math.PI/n;
            int cx=4*a<n||4*a>=3*n?mx-r:px+r,cy=2*a>n?py+r:my-r;
            Funcx(cx+r*Math.Cos(ra),cy+r*Math.Sin(ra),dr,Height2,draw,shape);
          }
          DrawLine2(px,py+r,px,my-r,false);
          DrawLine2(mx,py+r,mx,my-r,false);
          DrawLine2(px+r,py,mx-r,py,false);
          DrawLine2(px+r,my,mx-r,my,false);
          Repaint(px,py,mx,my,dr,true);
        }
        int[] Tria1(bool inv,bool move,int mx,int my) {
          int px=pix,py=piy,s;
          if(move) {pix=mx;piy=my;}
          if(inv) {s=mx;mx=px;px=s;s=my;my=py;py=s;}
          int dx=mx-px,dy=my-py,sx=mx+px,sy=my+py;
          if(GDI.CapsLock) {dx=-dx;dy=-dy;}
          return new int[] {px,py,(2*mx+dy)/2,(2*my-dx)/2,(2*mx-dy)/2,(2*my+dx)/2};
        }
        int[] Tria2(bool inv,bool move,int mx,int my) {
          int px=pix,py=piy,s;          
          if(inv) {s=mx;mx=px;px=s;s=my;my=py;py=s;}
          int dx=mx-px,dy=my-py,sx=mx+px,sy=my+py,ex=dx*14188/8192,ey=dy*14188/8182;          
          int[] p=new int[] {px,py,mx,my,mx+(-dx+ey)/2,my+(-dy-ex)/2};
          if(move) {pix=p[4];piy=p[5];}
          return p;
        }
    protected override void OnKeyDown(KeyEventArgs e) {
      if(e.KeyCode==Keys.Space) {
        e.SuppressKeyPress=true;
        e.Handled=true;        
      } else
        base.OnKeyDown(e);
    }
    protected override void OnKeyUp(KeyEventArgs e) {
      if(e.KeyCode==Keys.Space) {
        e.SuppressKeyPress=true;
        e.Handled=true;
      } else
        base.OnKeyDown(e);
    }
    static int CS(bool ctrl,bool shift,params int[] val) {
      int i=(ctrl?2:0)+(shift?1:0);
      return i<val.Length?val[i]:val[val.Length-1];
    }
    protected override bool ProcessCmdKey(ref Message msg,Keys keyData) {
          switch(keyData) {
            case Keys.ControlKey|Keys.Control:return false;
            case Keys.F11:Fullscreen();return true;
            case Keys.Escape:
              if(FormBorderStyle==FormBorderStyle.None) {
                Fullscreen();
              } else 
                NoScale();
              break;
          }          
          bool ctrl=0!=(keyData&Keys.Control);
          bool shift=0!=(keyData&Keys.Shift);
          Keys k=keyData&~(Keys.Shift|Keys.Control);
          if(ctrl&&(k<Keys.F1||k>Keys.F12)&&k!=Keys.OemPeriod&&k!=Keys.Oemcomma&&k!=Keys.Oem5&&k!=Keys.Oem2&&k!=Keys.Oem1) {
            switch(k) {
             case Keys.D1:Color2=shift?0x888888:0xffffff;break;
             case Keys.D2:Color2=shift?0x888800:0xffff00;break;
             case Keys.D3:Color2=shift?0x880088:0xff00ff;break;
             case Keys.D4:Color2=shift?0x008888:0x00ffff;break;
             case Keys.D5:Color2=shift?0x880000:0xff0000;break;
             case Keys.D6:Color2=shift?0x008800:0x00ff00;break;
             case Keys.D7:Color2=shift?0x000088:0x0000ff;break;
             case Keys.Q:SetCombine(Comb==Combine.None?1:0);break;
             case Keys.C:if(shift) using(var bm2=BMResized()) Clipboard.SetImage(bm2);else Clipboard.SetImage(bm);break;
             case Keys.V:Paste(IX(lmx,lmy),IY(lmx,lmy),GDI.ShiftLKey?-1:GDI.ShiftRKey?1:0,GDI.CtrlLKey?-1:GDI.CtrlRKey?1:0,GDI.CapsLock,GDI.Space);break;
             case Keys.B:PasteOrig(!shift);break;
             case Keys.W:Clear(shift);break;
             case Keys.O:OpenFile();Repaint(true);break;
             case Keys.S:SaveFile(GDI.ShiftKey);break;
             case Keys.N:NewFile();break;
             case Keys.P:if(shift) if(!PrintPage(true)) break;Print();break;
             case Keys.R:if(shift) AutoShrink();else Shrink(pix,piy,IX(lmx,lmy),IY(lmx,lmy));break;
             case Keys.E:if(shift) NoScale();else ExportFile(true);break;
             case Keys.Z:Undo();break;
             case Keys.Y:Redo();break;
             default:goto ret;
            }
            return true;
          } else {
            switch(k) {
              case Keys.F1:HelpCmd();break;
              case Keys.F2:SetHill(shift||ctrl?-2:-1);break;
              case Keys.F3:SetCombine(shift||ctrl?-2:-1);break;
              case Keys.F4:SetShape(shift||ctrl?-2:-1);break;              
              case Keys.D:Duplicate(pix,piy,IX(lmx,lmy),IY(lmx,lmy),shift);break;
              case Keys.V:Paste(IX(lmx,lmy),IY(lmx,lmy),GDI.ShiftLKey?-1:GDI.ShiftRKey?1:0,0,GDI.CapsLock,GDI.Space);break;
              case Keys.C:if(mseq>0) {if(shift) {pix=IX(lmx,lmy);piy=IY(lmx,lmy);} else DrawLine2(pix,piy,IX(lmx,lmy),IY(lmx,lmy));}else Copy(pix,piy,IX(lmx,lmy),IY(lmx,lmy)); break;
              case Keys.S:
               if(mseq>0) Snap(false,32,IX(lmx,lmy),IY(lmx,lmy));
               else {
                 pix=IX(lmx,lmy);piy=IY(lmx,lmy);
                 if(shift) map.SnapPoint(32,pix,piy,out pix,out piy);
                 undop=null;
               } break;
               case Keys.OemPeriod:case Keys.Oemcomma:case Keys.Oem2:case Keys.Oem1:
                 { bool cs=GDI.CapsLock||!GDI.NumLock;
                 Dotted(pix,piy,IX(lmx,lmy),IY(lmx,lmy)
                   ,k==Keys.Oem1?cs?CS(ctrl,shift,29,30,32,27):CS(ctrl,shift,25,26,28,31)
                   :k==Keys.Oem2?cs?CS(ctrl,shift,10,22,21,24):CS(ctrl,shift,7,8,9,18)
                   :k==Keys.Oemcomma?cs?CS(ctrl,shift,3,4,19,20):CS(ctrl,shift,1,16,17,6)
                   :cs?CS(ctrl,shift,12,15,14,13):CS(ctrl,shift,0,2,5,11));
                 } break;
               case Keys.F7:case Keys.Oem5:
                  { int x0=pix,y0=piy,x1=IX(lmx,lmy),y1=IY(lmx,lmy),dx=Math.Abs(x0-x1),dy=Math.Abs(y1-y0);
                    int c01,c10;bool ck=GDI.CtrlKey,sk=GDI.ShiftKey;
                    PushUndo(true,"gcolor");
                    if(ck) { c01=Color2;c10=Color3;}
                    else if(sk) { c01=Color3;c10=Color2;}
                    else { c01=c10=0;}
                    if(angle==0&&!(ck&&sk)) {
                      if(!ck&&!sk) {c01=hfmap.ColorMix(Color3,Color2,1,2);c10=hfmap.ColorMix(Color3,Color2,1,2);}
                      map.Color4(x0,y0,x1,y1,Color3,c01,c10,Color2);
                      hfmap.Sort(ref x0,ref y0,ref x1,ref y1);
                      Repaint(x0,y0,x1,y1,true);
                    } else {
                      int sx=SX(pix,piy),sy=SY(pix,piy);
                      int x2=IX(sx,lmy),y2=IY(sx,lmy);
                      if(!ck&&!sk) {
                        dx=hfmap.Dist(x2-x0,y2-y0);dy=hfmap.Dist(x1-x2,y1-y2);
                        //c01=hfmap.ColorMix(Color3,Color2,dy,dx+dy);c10=hfmap.ColorMix(Color3,Color2,dx,dx+dy);
                        c01=hfmap.ColorMix(Color3,Color2,1,2);c10=hfmap.ColorMix(Color3,Color2,1,2);
                      }
                      if(ck&&sk) {
                        int cx=(x0+x1)/2,cy=(y0+y1)/2,x3=x1-(x2-x0),y3=y1-(y2-y0);
                        map.GTriangle(x0,y0,cx,cy,x2,y2,Color2,Color3,Color2);
                        map.GTriangle(x0,y0,cx,cy,x3,y3,Color2,Color3,Color2);
                        map.GTriangle(x1,y1,cx,cy,x2,y2,Color2,Color3,Color2);
                        map.GTriangle(x1,y1,cx,cy,x3,y3,Color2,Color3,Color2);
                      } else
                        map.GRectangle(x0,y0,x2,y2,x1,y1,Color3,c01,Color2,c10);
                      hfmap.MinMax(ref x0,ref y0,ref x1,ref y1,x2,y2,x1-x2+x0,y1-y2+y0);
                      Repaint(x0,y0,x1,y1,true);
                    }
                  }
                break;
              case Keys.R:Rotate90(shift);break;
              case Keys.W:SetWhite(!White,true,true);break;
              case Keys.E:Extent(IX(lmx,lmy),IY(lmx,lmy));break;
              case Keys.M:Mirror(shift);break;              
              case Keys.I:case Keys.N:case Keys.B:case Keys.G:case Keys.X:
              case Keys.T:case Keys.H:case Keys.J:case Keys.K:              
              case Keys.U:case Keys.P:
              case Keys.O:case Keys.Q:case Keys.Z:
              case Keys.L:
                if(mseq<1) PushUndo(true,k==Keys.I?"rect":k==Keys.N?"norm":k==Keys.B?"iso":null);
                int mx=IX(lmx,lmy),my=IY(lmx,lmy);
                int[] p=null;
                if(k==Keys.O) {
                  ShapeO(shift,GDI.NumLock,mx,my); 
                } else if(k==Keys.Z) {
                  ShapeR(shift,mx,my);
                } else if(k==Keys.Q) {
                  ShapeQ(shift,GDI.NumLock,mx,my); 
                } else if(k==Keys.G) {
                  ShapeG(shift,mx,my); 
                } else if(k==Keys.U) {
                  ShapeU(shift,GDI.NumLock,mx,my); 
                } else if(k==Keys.P) {
                  ShapeP(shift,GDI.CapsLock,GDI.NumLock,mx,my);
                } else if(k==Keys.T) {
                  if(shift) p=Tria2(!GDI.CapsLock,GDI.NumLock,mx,my);
                  else p=Tria1(GDI.CapsLock,GDI.NumLock,mx,my);
                } else if(k==Keys.H) {
                  if(shift) {
                    int dx=mx-pix,dy=my-piy,sx=mx+pix,sy=my+piy,ex=dx*14188/8192,ey=dy*14188/8182;
                    p=new int[] {pix,piy,mx,my,mx+(dx+ey)/2,my+(dy-ex)/2,mx+ey,my-ex,pix+ey,piy-ex,pix-(dx-ey)/2,piy-(dy+ex)/2};
                    pix=p[4];piy=p[5];
                  } else {
                    int dx=mx-pix,dy=my-piy,sx=mx+pix,sy=my+piy,ex=dx*14188/16384,ey=dy*14188/16384;
                    p=new int[] {pix,piy,pix+(dx-2*ey)/4,piy+(dy+2*ex)/4,mx-(dx+2*ey)/4,my-(dy-2*ex)/4,mx,my,mx-(dx-2*ey)/4,my-(dy+2*ex)/4,pix+(dx+2*ey)/4,piy+(dy-2*ex)/4};
                    pix=mx;piy=my;
                  }
                } else if(k==Keys.X) {
                  if(angle==0) {
                    map.Clear(pix,piy,mx,my,0,White?Pal.White:0,false);
                    { int x0=pix,y0=piy,x1=mx,y1=my;
                      hfmap.Sort(ref x0,ref y0,ref x1,ref y1);                  
                      Repaint(x0,y0,x1,y1,true);
                    }
                  } else {
                     int sx=SX(pix,piy),sy=SY(pix,piy);
                     int x2=IX(sx,lmy),y2=IY(sx,lmy);
                     int x0=pix,y0=piy,x1=IX(lmx,lmy),y1=IY(lmx,lmy),dx=Math.Abs(x0-x1),dy=Math.Abs(y1-y0);
                      map.Rectangle(x0,y0,x2,y2,x1,y1,0,White?Pal.White:0,false);
                      hfmap.MinMax(ref x0,ref y0,ref x1,ref y1,x2,y2,x1-x2+x0,y1-y2+y0);
                      Repaint(x0,y0,x1,y1,true);
                  }
                } else if(k==Keys.I) {
                    if(angle==0) {
                      if(shift) {                   
                        map.Clear(pix,piy,mx,my,0,White?Pal.White:0,false);
                        { int x0=pix,y0=piy,x1=mx,y1=my,dr=Radius-1;
                          hfmap.Sort(ref x0,ref y0,ref x1,ref y1);
                          x0+=dr;y0+=dr;x1-=dr;y1-=dr;                        
                          Repaint(x0,y0,x1,y1,true);
                        }
                      }
                      p=new int[] {pix,piy,mx,piy,mx,my,pix,my};
                    } else {
                     int sx=SX(pix,piy),sy=SY(pix,piy);
                     int x2=IX(sx,lmy),y2=IY(sx,lmy);
                     int x0=pix,y0=piy,x1=IX(lmx,lmy),y1=IY(lmx,lmy),dx=Math.Abs(x0-x1),dy=Math.Abs(y1-y0);
                     p=new int[] {x0,y0,x2,y2,x1,y1,x1-x2+x0,y1-y2+y0};
                     if(shift) {
                      map.Rectangle(x0,y0,x2,y2,x1,y1,0,White?Pal.White:0,false);
                      hfmap.MinMax(ref x0,ref y0,ref x1,ref y1,x2,y2,x1-x2+x0,y1-y2+y0);
                      Repaint(x0,y0,x1,y1,true);
                     }                     
                    }
                } else if(k==Keys.K) {
                  if(shift) {
                    int dx=mx-pix,dy=my-piy,sx=mx+pix,sy=my+piy;
                    p=new int[] {pix,piy,mx,my,mx+dy,my-dx,pix+dy,piy-dx};
                    pix=p[4];piy=p[5];
                  } else {
                    int dx=mx-pix,dy=my-piy,sx=mx+pix,sy=my+piy;
                    p=new int[] {pix,piy,(sx-dy)/2,(sy+dx)/2,mx,my,(sx+dy)/2,(sy-dx)/2};
                    pix=mx;piy=my;
                  }
                } else if(k==Keys.J) {
                  if(shift) {
                    int dx=mx-pix,dy=my-piy,ex=dx*181/256,ey=dy*181/256;
                    p=new int[] {pix,piy,mx,my,mx+ex+ey,my+ey-ex,mx+ex+ey+dy,my+ey-ex-dx,mx+2*ey+dy,my-2*ex-dx,pix+2*ey+dy,piy-2*ex-dx,pix-ex+ey+dy,piy-ey-ex-dx,pix-ex+ey,piy-ey-ex};
                    pix=p[10];piy=p[11];
                  } else {
                    int dx=mx-pix,dy=my-piy,sx=mx+pix,sy=my+piy,ex=dx*181/256,ey=dy*181/256;
                    p=new int[] {pix,piy,(sx-ex-ey)/2,(sy-ey+ex)/2,(sx-dy)/2,(sy+dx)/2,(sx+ex-ey)/2,(sy+ey+ex)/2,mx,my,(sx+ex+ey)/2,(sy+ey-ex)/2,(sx+dy)/2,(sy-dx)/2,(sx-ex+ey)/2,(sy-ey-ex)/2};
                    pix=mx;piy=my;
                  }
                } else if(k==Keys.N||k==Keys.B) {
                  int dx=mx-pix,dy=my-piy,nx=pix,ny=piy;
                  if(k==Keys.B) {
                    if(shift) {
                      int sx=(mx+pix)/2,ey=(dy<0?-1:1)*Math.Abs(dx/4);
                      DrawLine2(pix,piy,sx,piy-ey);
                      DrawLine2(sx,piy-ey,mx,piy);
                      DrawLine2(pix,piy,pix,my);
                      DrawLine2(sx,piy-ey,sx,my-ey);
                      DrawLine2(mx,piy,mx,my);
                      DrawLine2(pix,my,sx,my-ey);
                      DrawLine2(sx,my-ey,mx,my);
                      DrawLine2(pix,my,sx,my+ey);
                      DrawLine2(sx,my+ey,mx,my);
                      pix=mx;
                      goto b;
                    } else if(Math.Abs(2*dx)>Math.Abs(dy)) {
                      int e=Math.Abs(2*dy)>Math.Abs(dx)?dx:2*dy;
                      nx=pix+(int)Sgn(dx,e);ny=piy+(int)Sgn(dy,e/2);
                    } else
                      ny=my;
                  } else if(Math.Abs(dx)>=Math.Abs(dy)) {
                    if(shift) {
                      nx=pix+(int)Sgn(dx,dy);ny=my;
                    } else
                      nx=mx;
                  } else {
                    if(shift) {
                      nx=mx;ny=piy+(int)Sgn(dy,dx);
                    } else
                      ny=my;
                  }
                  DrawLine2(pix,piy,nx,ny);
                  pix=nx;piy=ny;
                 b:;
                } else {
                  DrawLine2(pix,piy,mx,my);
                  if(!GDI.ShiftKey) { pix=mx;piy=my;}
                }
                if(p!=null) for(int i=0,j=p.Length-2;i<p.Length;j=i,i+=2)
                  DrawLine2(p[i],p[i+1],p[j],p[j+1]);
                if(mseq==0) map.ApplyDraw(Color2,Height2,Comb,draw,true,White,0);
                break;
              case Keys.F:
                PushUndo(true);
                map.FloodFill(IX(lmx,lmy),IY(lmx,lmy), (byte)(Height2 - 10), Color2);
                Repaint(true);
                break;
              default: goto ret;
            }
            return true;
          } 
         ret:
          return base.ProcessCmdKey(ref msg,keyData);
        }
        void DrawLine(int x0,int y0,int x1,int y1) {
          DrawLine2(IX(x0,y0),IY(x0,y0),IX(x1,y1),IY(x1,y1),true);
        }
        void DrawLine2(int x0,int y0,int x1,int y1) { DrawLine2(x0,y0,x1,y1,true);}
        void DrawLine2(int x0,int y0,int x1,int y1,bool repaint) {
          int r=Radius;
          if(Shape==1) map.FuncSquareLine(x0,y0,x1,y1,r,Height2,draw,shape);
          else if(Shape==2) map.FuncDiamondLine(x0,y0,x1,y1,r,Height2,draw,shape);
          else map.FuncLine(x0,y0,x1,y1,r,Height2,draw,shape);
          int s;
          if(x0>x1) {s=x0;x0=x1;x1=s;}
          if(y0>y1) {s=y0;y0=y1;y1=s;}
          if(repaint) Repaint(x0-r,y0-r,x1+r,y1+r,true);        
        }
        void Dotted(int x0,int y0,int x1,int y1,int mode) {
          int r=Radius;
          if(r<2||(x0==x1&&y0==y1)) return;
          PushUndo(true,"dot."+mode);
          Array.Clear(draw,0,draw.Length);
          int n=2,e=mode>1?r:0;
          for(int i=2*r,i2=i*i,dx=x1-x0,dy=y1-y0,d2=dx*dx+dy*dy,d=(int)Math.Sqrt(d2),kx=r*dx/d,ky=r*dy/d;i2<=d2;i2+=2*i*n*r+n*n*r*r,i+=n*r)  {
            int ox=pix,oy=piy,lx,ly;
            pix=x0+i*dx/d;piy=y0+i*dy/d;            
            if(mode==32) {
              int mx=(ox+pix)/2,my=(oy+piy)/2;
              DrawLine2(mx-ky,my+kx,mx+ky,my-kx,false);
            } else if(mode==31) {
              int mx=(ox+pix)/2,my=(oy+piy)/2;
              DrawLine2(mx-ky,my+kx,lx=ox,ly=oy,false);
              DrawLine2(lx,ly,mx+ky,my-kx,false);
            } else if(mode==30) {
              int mx=(ox+pix)/2,my=(oy+piy)/2;
              Funcx(pix-ky/2,piy+kx/2,r,Height2,draw,shape);
              Funcx(mx-ky/2,my+kx/2,r,Height2,draw,shape);
              Funcx(pix+ky/2,piy-kx/2,r,Height2,draw,shape);
              Funcx(mx+ky/2,my-kx/2,r,Height2,draw,shape);
            } else if(mode==29) {
              int mx=(ox+pix)/2,my=(oy+piy)/2;
              DrawLine2(pix-ky/2,piy+kx/2,mx-ky/2,my+kx/2,false);
              DrawLine2(pix+ky/2,piy-kx/2,mx+ky/2,my-kx/2,false);
            } else if(mode==28) {
              int mx=(ox+pix)/2,my=(oy+piy)/2;
              DrawLine2(pix-ky,piy+kx,lx=mx,ly=my,false);
              DrawLine2(lx,ly,pix+ky,piy-kx,false);
              DrawLine2(ox,oy,mx,my,false);
            } else if(mode==25||mode==26||mode==27) {
              int mx=mode==26?pix:(ox+pix)/2,my=mode==26?piy:(oy+piy)/2;
              DrawLine2(mx-ky,my+kx,lx=ox,ly=oy,false);
              DrawLine2(lx,ly,lx=ox,ly=oy,false);
              DrawLine2(lx,ly,lx=mx+ky,ly=my-kx,false);
              if(mode==27) DrawLine2(ox,oy,mx,my,false);
              else DrawLine2(ox,oy,pix,piy,false);
            } else if(mode==24) {
              DrawLine2(ox-ky,oy+kx,lx=pix-ky,ly=piy+kx,false);
              DrawLine2(lx,ly,lx=pix+ky,ly=piy-kx,false);
              DrawLine2(lx,ly,lx=ox+ky,ly=oy-kx,false);
              DrawLine2(lx,ly,ox-ky,oy+kx,false);
            } else if(mode==23) {
              DrawLine2(ox+kx-ky/2,oy+ky+kx/2,lx=pix-ky/2,ly=piy+kx/2,false);
              DrawLine2(lx,ly,lx=pix+ky/2,ly=piy-kx/2,false);
              DrawLine2(lx,ly,lx=ox+kx+ky/2,ly=oy+ky-kx/2,false);
              DrawLine2(lx,ly,ox+kx-ky/2,oy+ky+kx/2,false);
            } else if(mode==22) {
              DrawLine2(ox+5*kx/4-ky/4,oy+5*ky/4+kx/4,ox+7*kx/4+ky/4,oy+7*ky/4-kx/4,false);
              DrawLine2(ox+5*kx/4+ky/4,oy+5*ky/4-kx/4,ox+7*kx/4-ky/4,oy+7*ky/4+kx/4,false);
            } else if(mode==21) {
              DrawLine2(ox+2*kx/2-ky/2,oy+2*ky/2+kx/2,ox+4*kx/2+ky/2,oy+4*ky/2-kx/2,false);
              DrawLine2(ox+2*kx/2+ky/2,oy+2*ky/2-kx/2,ox+4*kx/2-ky/2,oy+4*ky/2+kx/2,false);
            } else if(mode==19||mode==20) {
              Funcx(pix,piy,r,Height2,draw,shape);
              Funcx(ox+kx-ky,oy+ky+kx,r,Height2,draw,shape);
              if(mode==20) Funcx(ox+kx+ky,oy+ky-kx,r,Height2,draw,shape);
            } else if(mode==18) {
              DrawLine2(ox,oy,lx=pix+ky,ly=piy-kx,false);
              DrawLine2(lx,ly,lx=pix-ky,ly=piy+kx,false);
              DrawLine2(lx,ly,ox,oy,false);
            } else if(mode==17) {
              DrawLine2(ox,oy,pix,piy,false);
              DrawLine2(ox+kx/2-ky,oy+ky/2+kx,ox+kx/2+ky,oy+ky/2-kx,false);
            } else if(mode==16) {
              DrawLine2(ox,oy,pix,piy,false);
              DrawLine2(ox+kx/2-ky/2,oy+ky/2+kx/2,ox+kx/2+ky/2,oy+ky/2-kx/2,false);              
            } else if(mode==14||mode==15) {
              Circle(pix,piy,r/2);
              if(mode==15) Circle(ox+kx,oy+ky,r/2);
              else DrawLine2(ox+kx/2,oy+ky/2,pix-kx/2,piy-ky/2,false);
            } else if(mode==12||mode==13) {
              Funcx(pix,piy,r,Height2,draw,shape);
              Funcx(ox+kx,oy+ky,r,Height2,draw,shape);
              if(mode==13) {
                Funcx(ox+kx/2-ky,oy+ky/2+kx,r,Height2,draw,shape);
                Funcx(ox+kx/2+ky,oy+ky/2-kx,r,Height2,draw,shape);
              }
            } else if(mode==11) {
              //Circle((ox+3*pix)/4,(oy+3*piy)/4,r);
              Circle(pix,piy,r);
            } else if(mode==10) {
              DrawLine2(ox,oy,lx=ox+kx-ky,ly=oy+ky+kx,false);
              DrawLine2(lx,ly,lx=pix,ly=piy,false);
              DrawLine2(lx,ly,lx=ox+kx+ky,ly=oy+ky-kx,false);
              DrawLine2(lx,ly,ox,oy,false);
            } else if(mode==8||mode==9||mode==7) {
              if(mode==7) DrawLine2(ox,oy,lx=ox+kx-ky,ly=oy+ky+kx,false);
              else if(mode==8) DrawLine2(ox,oy,lx=ox-ky,ly=oy+kx,false);
              else DrawLine2(ox,oy,lx=pix-ky,ly=piy+kx,false);              
              DrawLine2(lx,ly,pix,piy,false);
            } else if(mode==6) {
              DrawLine2(ox,oy,lx=ox-ky/2,ly=oy+kx/2,false);
              DrawLine2(lx,ly,lx=ox+kx-ky/2,ly=oy+ky+kx/2,false);
              DrawLine2(lx,ly,lx=ox+kx+ky/2,ly=oy+ky-kx/2,false);
              DrawLine2(lx,ly,lx=pix+ky/2,ly=piy-kx/2,false);
              DrawLine2(lx,ly,pix,piy,false);
            } else if(mode>1) {
              for(int j=0;j<n*r;j++) {
                int k=j%r-r/2,l=j;
                if(mode==5) {l=j/r*r;}
                else if(mode==3||mode==4) k=(int)(r*Math.Sin(j*Math.PI/r)/(mode==4?2:4));
                else k=j%r-r/2;
                Funcx(ox+(l*dx-k*dy)/d,oy+(k*dx+l*dy)/d,r,Height2,draw,shape);
              }
            } else if(mode==1) 
              DrawLine2((pix+ox)/2,(piy+oy)/2,pix,piy,false);
            else
              Funcx(pix,piy,r,Height2,draw,shape);
          }          
          hfmap.Sort(ref x0,ref y0,ref x1,ref y1);
          Repaint(x0,y0,x1,y1,r+e,true);
          map.ApplyDraw(Color2,Height2,Comb,draw,true,White,0);
        }
        
        void Snap(bool auto,int size,int x,int y) {
          if(auto&&0>=map.GetHeight(x,y)) return;
          int sx,sy;
          if(map.SnapPoint(size,x,y,out sx,out sy)) {
            DrawLine(SX(sx,sy),SY(sx,sy),SX(x,y),SY(x,y));
          }        
        }
        static Combine InvComb(Combine x) {
          int i=(int)x;
          return (Combine)(i>=1&&i<=6?1+((i-1)^1):i);
        }
        static Combine SubComb(Combine x,bool sub) {
           if(x==Combine.Max||x==Combine.Min) x=sub?Combine.Min:Combine.Max;
           else if(x==Combine.Mul||x==Combine.Div) x=sub?Combine.Div:Combine.Mul;            
           else if(x==Combine.Add||x==Combine.Sub) x=sub?Combine.Sub:Combine.Add;
           return x;
        }
        private void fMain_MouseDown(object sender,MouseEventArgs e) {
          bool lb=0!=(e.Button&MouseButtons.Left),rb=0!=(e.Button&MouseButtons.Right);
          if(pmb!=MouseButtons.None) return;
          if(!lb&&!rb&&GDI.ShiftKey&&GDI.CtrlKey) {
            SetAngle(e.X,e.Y,0);
            Repaint(false);
            return;
          }
          int cx,cy;
          pix=cx=IX(e.X,e.Y);piy=cy=IY(e.X,e.Y);
          if((lb||rb)&&(AutoSnap||GDI.ShiftKey))
            map.SnapPoint(32,pix,piy,out pix,out piy);
          if(lb||rb) {
            Comb=SubComb(Comb,rb);            
            PushUndo(true);
            Array.Clear(draw,0,draw.Length);              
            int r=Radius;
            Funcx(cx,cy,r,Height2,draw,shape);
            mseq=1;
            Repaint(cx-r,cy-r,cx+r,cy+r,true);
            if(pix!=cx||piy!=cy) DrawLine2(pix,piy,cx,cy);            
          } else if(0!=(e.Button&MouseButtons.Middle)) {
            
          }
          lmx=e.X;lmy=e.Y;
          pmb=e.Button;
          pmk=(GDI.ShiftKey?1:0)|(GDI.CtrlKey?2:0);
        }
        
        private void fMain_MouseMove(object sender, MouseEventArgs e) {
          if(e.Button==pmb) {
            MouseButtons mb=Comb==Combine.Max?MouseButtons.Left:Comb==Combine.Min?MouseButtons.Right:MouseButtons.Left|MouseButtons.Right;
            if(0!=(e.Button&mb)&&mseq>0) {              
              DrawLine(lmx,lmy,e.X,e.Y);
            }
            if(0!=(pmb&MouseButtons.Middle)) {
              if(0==(3&pmk)) {
                sx+=e.X-lmx;sy+=e.Y-lmy;
                timeDraw=true;
              }
            }           
          }
          lmx=e.X;lmy=e.Y;
        }
        private void fMain_MouseUp(object sender,MouseEventArgs e) {
          if(e.Button!=pmb) return;
          MouseButtons mb=Comb==Combine.Max?MouseButtons.Left:Comb==Combine.Min?MouseButtons.Right:MouseButtons.Left|MouseButtons.Right;
          if(0!=(pmb&MouseButtons.Middle)) {
            if(0!=(3&pmk)) {
              ZoomTo(pix,piy,IX(e.X,e.Y),IY(e.X,e.Y),0!=(pmk&1),3==(pmk&3));
              timeDraw=true;
            } else {
              sx+=e.X-lmx;sy+=e.Y-lmy;
              timeDraw=true;
            } 
          } else if(0!=(e.Button&mb)) {
            if(mseq>0) {
              int ix=IX(e.X,e.Y),iy=IY(e.X,e.Y);
              pix=ix;piy=iy;
              if(AutoSnap||GDI.ShiftKey) {
                if(map.SnapPoint(32,ix,iy,out pix,out piy))
                  DrawLine2(ix,iy,pix,piy);
              }
              map.ApplyDraw(Color2,Height2,Comb,draw,true,White,0);
              mseq=0;
            }
          }
          lmx=e.X;lmy=e.Y;pmb=MouseButtons.None;
        }
        void ZoomTo(int x0,int y0,int x1,int y1,bool bigger,bool z100) {
          Rectangle cr=ClientRectangle;
          int r;
          if(x1<x0) {r=x1;x1=x0;x0=r;}
          if(y1<y0) {r=y1;y1=y0;y0=r;}
          x1-=x0;y1-=y0;
          if(x1==0&&y1==0) {
            x0=y0=0;x1=map.Width-1;y1=map.Height-1;
          }
          x1++;y1++;
          int zx=cr.Width*ZoomBase/x1,zy=cr.Height*ZoomBase/y1;
          if(zy<zx^bigger) zx=zy;
          if(zx>ZoomBase*16) zx=ZoomBase*16;
          else if(zx<12) zx=12;
          zoom=z100?ZoomBase:zx;
          sx=cr.Width/2-(x0+x1/2)*zoom/ZoomBase;
          sy=cr.Height/2-(y0+y1/2)*zoom/ZoomBase;
        }       
        
        bool CheckDirty(string caption) {
          if (!Dirty) return true;
          DialogResult dr=MessageBox.Show(this,"Save changes?",caption,MessageBoxButtons.YesNoCancel,MessageBoxIcon.Exclamation,MessageBoxDefaultButton.Button3);
          if(dr!=DialogResult.Yes) return dr==DialogResult.No;
          return SaveFile(false);
        }
        void SetDirty() {
          if(Dirty) return;
          Dirty=true;
          if(!Text.EndsWith("*")) Text+="*";
        }
        void UnsetDirty() {
          if(!Dirty) return;
          Dirty=false;
          if(Text.EndsWith("*")) Text=Text.Substring(0,Text.Length-1);        
        }

        private void miFileClear_Click(object sender, EventArgs e) {
          map.Clear(0,Pal.White,false);
          Repaint(true);
        }
        
        void ChangeFileName(string filename) {
          ofd.FileName=filename;
          string fn=Path.GetFileName(filename);
          Dirty=false;
          Text="BlackBoard"+(string.IsNullOrEmpty(fn)?"":" - "+fn);//+(Dirty?"*":"");
        }
        void LoadFile(string filename,bool update) {
          if(!File.Exists(filename)) return;
          bool white;
          map=hfmap.Load(map,filename,out white);
          ChangeFileName(filename);
          ClearUndo();                      
          SetWhite(white,false,false);
          if(update) {
            UpdateBitmap();
            Repaint(true);
          }          
        }
        bool SaveFile(bool saveas) {
          if(ofd.FileName+""==""||saveas) {
            if(sfd==null) sfd=new SaveFileDialog();
            sfd.FileName=ofd.FileName;
            sfd.Filter="*.bb|*.bb|*.*|*.*";
            sfd.DefaultExt="bb";
            sfd.Title=saveas?"Save as":"Save";
            if(DialogResult.OK!=sfd.ShowDialog()) return false;
            string fname=sfd.FileName;
            if(Path.GetExtension(fname)=="") fname+=".bb";
            ChangeFileName(sfd.FileName);
          }
          map.Save(ofd.FileName,White);
          UnsetDirty();
          return true;
        }
        bool ExportFile(bool dialog) {   
          if(efd==null) {
            efd=new SaveFileDialog();
            if(ofd.FileName+""!="") efd.FileName=Path.GetFileNameWithoutExtension(ofd.FileName)+".png";
          }
          if(dialog||efd.FileName+""=="") {
            efd.Filter="png (shift - alpha channel, CapsLock - view size)|*.png|grayscale height png|*.png|pgm height|*.pgm|*.*|*.*";
            efd.DefaultExt="png";
            efd.Title="Export";
            if(DialogResult.OK!=efd.ShowDialog()) return false;
          }          
          ExportFile(efd.FileName,GDI.ShiftKey,GDI.CapsLock,efd.FilterIndex);
          return true;
        }

        void ExportFile(string file,bool alpha,bool zoo,int fi) {
          var m=map;
          if(zoo) {
            m=map.Resize(map.Width*zoom/ZoomBase,map.Height*zoom/ZoomBase);
          }       
          if(fi==3) m.ExportPgm(file,White);
          else if(fi==2) m.ExportGrayPng(file,White);
          else m.ExportPng(file,GDI.ShiftKey,White);
        }        
        bool PrintPage(bool show) {
          if(paged==null) {
            paged=new PageSetupDialog();
            paged.EnableMetric=true;
            paged.AllowPaper=paged.AllowMargins=paged.AllowOrientation=true;
            paged.PageSettings=new System.Drawing.Printing.PageSettings() {Landscape=true,Margins=new System.Drawing.Printing.Margins(0,0,0,0)};
          }          
          return show&&paged.ShowDialog(this)==DialogResult.OK;
        }
        void Print() {
          PrintPage(false);
          if(pd==null) pd=new PrintDialog();
          pd.PrinterSettings.DefaultPageSettings.Landscape=paged.PageSettings.Landscape;
          if(pd.ShowDialog(this)==DialogResult.OK) {
            using(System.Drawing.Printing.PrintDocument doc=new System.Drawing.Printing.PrintDocument()) {
              paged.PageSettings.Landscape=pd.PrinterSettings.DefaultPageSettings.Landscape;
              doc.DocumentName=ofd==null?"blackboard":Path.GetFileName(ofd.FileName);
              doc.PrinterSettings=pd.PrinterSettings;
              doc.DefaultPageSettings=paged.PageSettings.Clone() as System.Drawing.Printing.PageSettings;
              doc.PrintPage+=new System.Drawing.Printing.PrintPageEventHandler(doc_PrintPage);
             try {
              doc.Print();
             } catch(Exception ex) {
               MessageBox.Show(this,ex.Message,"Exception",MessageBoxButtons.OK);
             }
            }
          }
        }
        void doc_PrintPage(object sender,System.Drawing.Printing.PrintPageEventArgs e) {
          Graphics gr=e.Graphics;
          Rectangle rect=e.MarginBounds;
          if(rect.Width*bm.Height>rect.Height*bm.Width) {
            int w=rect.Height*bm.Width/bm.Height,sx=(rect.Width-w)/2;
            rect.X+=sx;rect.Width=w;
          } else {
            int h=rect.Width*bm.Height/bm.Width,sy=(rect.Height-h)/2;
            rect.Y+=sy;rect.Height=h;
          }
          gr.DrawImage(bm,rect);
          e.HasMorePages=false;
        }

        void NewFile() {
          if(!CheckDirty("New file")) return;          
          ChangeFileName(null);
          NewMap(true);
        }
        void NewMap(bool update) {
          ClearUndo();
          if(NewWidth==0||NewHeight==0) {
            NewWidth=Screen.PrimaryScreen.Bounds.Width;
            NewHeight=Screen.PrimaryScreen.Bounds.Height;
          }
          map=new hfmap(NewWidth,NewHeight);
          if(update) {UpdateBitmap();Repaint(true);}        
        }
        void OpenFile() {
          if(!CheckDirty("Open file")) return;
          string dir=Directory.GetCurrentDirectory();
          ofd.Title="Open";
          ofd.Filter="*.bb|*.bb|*.*|*.*";
          ofd.DefaultExt="bb";
          if(DialogResult.OK==ofd.ShowDialog(this)) {
            ClearUndo();
            LoadFile(ofd.FileName,true);
          }
          Directory.SetCurrentDirectory(dir);
        }
        protected override void OnClosing(CancelEventArgs e) {
          if(!CheckDirty("Close window")) e.Cancel=true;
        }
                

        private void miHill_Click(object sender, EventArgs e) {
          SetHill(int.Parse(GetTag(sender)));
        }
        void SetHill(int tag) {
          ToolStripMenuItem x;          
          if(tag==-1||tag==-2) {
            Image hi=miHill.Image;
            int i=hi==miHillNail.Image?4:hi==miHillSigma.Image?3:hi==miHillCylinder.Image?2:hi==miHillSphere.Image?1:0;
            tag=(i+(tag==-1?1:4))%5;
          }
          switch(tag) {
           case 2:Bez=BezCylinder;x=miHillCylinder;break;
           case 4:Bez=BezNail;x=miHillNail;break;
           case 1:Bez=BezSphere;x=miHillSphere;break;
           case 3:Bez=BezSigma;x=miHillSigma;break;
           default:Bez=BezCone;x=miHillCone;break;
          }
          Bez.Shape(shape);
          miHill.Image=bHill.Image=x.Image;
          return;
        }

        private void miShape_Click(object sender, EventArgs e) {
          SetShape(int.Parse(GetTag(sender)));
        }
        void SetShape(int i) {
          ToolStripMenuItem x;
          if(i==-1||i==-2) i=(Shape+(i==-1?1:2))%3;
          switch(i) {
           case 1:Shape=1;x=miShapeSquare;break;
           case 2:Shape=2;x=miShapeDiamond;break;
           default:Shape=0;x=miShapeCircle;break;
          }
          miShape.Image=bShape.Image=x.Image;
          return;
        }

        void GetUndo(UndoItem u) {
          u.sx=sx;u.sy=sy;u.zoom=zoom;u.angle=angle;
          u.pix=pix;u.piy=piy;
        }

        void SwapUndo(UndoItem u) {
          int r;
          r=sx;sx=u.sx;u.sx=r;r=sy;sy=u.sy;u.sy=r;
          r=zoom;zoom=u.zoom;u.zoom=r;
          r=angle;angle=u.angle;u.angle=r;
          r=pix;pix=u.pix;u.pix=r;r=piy;piy=u.piy;u.piy=r;
          UpdateSin();
          hfmap x=map;
          map=u.map;
          u.map=x;            
        }

        internal void PushUndo(bool clone) { PushUndo(clone,null);}
        internal void PushUndo(bool clone,string op) {
          if(op!=null&&op==undop) return;
          undop=op;
          UndoItem ui=undoc<undos.Count?undos[undoc]:new UndoItem();
          ui.map=clone?map.Clone():map;
          GetUndo(ui);
          undos.RemoveRange(undoc,undos.Count-undoc);
          if(undoc>UndoMax) {
            undos.RemoveAt(0);
            GC.Collect();
          }
          undos.Add(ui);
          undoc=undos.Count;          
          SetDirty();
        }

        public void Undo(bool redo) {
          if(redo) {
            if(undoc>=undos.Count) return;
            UndoItem ui=undos[undoc];
            SwapUndo(ui);
            undoc++;
          } else {
            if(undoc<1) return;
            undoc--;
            UndoItem ui=undos[undoc];
            SwapUndo(ui);
          }
          CheckBitmap();
          Repaint(true);
        }

        internal void Undo() { Undo(false);}
        internal void Redo() { Undo(true);}
        internal void ClearUndo() { undos.Clear();undoc=0;}
        
        private void miEdit_Click(object sender, EventArgs e) {
          var mi = sender as ToolStripMenuItem;
          switch(mi.Tag+"") {
           case "redo":Redo();return;
           case "undo":Undo();return;
           case "clear":
            map.Clear(0,Pal.White,false);
            break;
           case "blur":
            PushUndo(false);
            map=map.Blur(3);
            break;
           case "invert":
            map.Invert(false,true);
            map.ZeroReplace(White);
            break;
           case "layers":
            map.FuncHeight(x=>(byte)(x==0?0:1+(x/32*254/7)));
            break;
          }
          Repaint(true);
        }

        string GetTag(object sender) {
          ToolStripMenuItem i = sender as ToolStripMenuItem;
          Button b;
          if(i!=null) return ""+i.Tag;
          else if((b=sender as Button)!=null) return ""+b.Tag;
          else return "";
        }
        private void miCombine_Click(object sender, EventArgs e) {
          string tag=GetTag(sender);
          if(tag!="") SetCombine(int.Parse(tag));
        }

        void SetCombine(int value) {          
          if(value==-1) value=(int)(Comb==Combine.Max?Combine.Mul:Comb==Combine.Mul?Combine.Add:Combine.Max);
          else if(value==-2) value=(int)(Comb==Combine.Max?Combine.Add:Comb==Combine.Mul?Combine.Max:Combine.Mul);
          Comb=(Combine)value;
          ToolStripMenuItem i=Comb==Combine.Add?miCombineAdd:Comb==Combine.Mul?miCombineMul:Comb==Combine.None?miCombineNone:miCombineMax;
          miCombine.Image=bCombine.Image=i.Image;
        }

        private void fMain_Resize(object sender,EventArgs e) {
          Repaint(false);
        }

        protected override void OnPaintBackground(PaintEventArgs e) {
          //base.OnPaintBackground(e);
        }
        protected override void OnPaint(PaintEventArgs e) {
          //base.OnPaint(e);
          //Repaint(e.Graphics,e.ClipRectangle.Left,e.ClipRectangle.Top,e.ClipRectangle.Width,e.ClipRectangle.Height);
          timeDraw=true;
        }        

        private void timer_Tick(object sender,EventArgs e) {
          if(timeDraw) {
            Repaint(false);
            timeDraw=false;
          }
        }
        int ColorIdx(int idx,bool shift) {
          idx=idx*2+(shift?1:0)-1;
          switch(idx) {
           case 1:return 0xff0000;
           case 2:return 0xff0088;
           case 3:return 0xffff00;
           case 4:return 0xff8800;
           case 5:return 0x00ff00;
           case 6:return 0x88ff00;
           case 7:return 0x00ffff;
           case 8:return 0x00ff88;
           case 9:return 0x0000ff;
           case 10:return 0x0088ff;
           case 11:return 0xff00ff;
           case 12:return 0x8800ff;
           case 13:return 0xffffff;
           case 14:return 0xcccccc;
           case 15:return 0x888888;
           case 16:return 0x444444;
           default:return 0;
          }
        }
        void SetColor(int x) {
          Color3=Color2;
          bColor.BackColor=ColorInt(Color2=x);
        }
        static Color ColorInt(int color) {return Color.FromArgb(color|(255<<24));}

            private void bColor_MouseUp(object sender, MouseEventArgs e) {
          bool bc=sender==bColor,sh=GDI.ShiftKey,ct=GDI.CtrlKey,ns=sh||ct;
          if(bc) {
            ns=ct;
            if(sh) bColor.BackColor=Pal.IntColor(Color3);
            else ColorDiag();            
          }
          Button b=sender as Button;
          int color=Pal.IntColor(b.BackColor);
          if(e.Button!=MouseButtons.Left) {
            int c=Pal.IntColor(bColor.BackColor);
            color=Pal.MixColor(c,color,e.Button==MouseButtons.Right?64:128,256);
          }
          if(ns) bColor.BackColor=Pal.IntColor(Color2);
          else SetColor(color);
          if(color==0) color=Pal.White;
          bool shift=GDI.ShiftKey;
          int rgbs=Pal.RGBSum(color);
          bool grx=b==bColorGr1||b==bColorGr2||b==bColorGr3||b==bColorGr4;
          if(!bc&&(sh&&!ct)) color=Pal.ColorIntensity765(color,765-(765-rgbs)/2);
          else if(!bc&&(ct&&!sh)) color=Pal.ColorIntensity765(color,rgbs/2);
          if(!grx) {
            bColorGr1.BackColor=Pal.IntColor(Pal.ColorIntensity765(color,765-(765-rgbs)/2));
            bColorGr2.BackColor=Pal.IntColor(Pal.ColorIntensity765(color,rgbs*3/4));
            bColorGr3.BackColor=Pal.IntColor(Pal.ColorIntensity765(color,rgbs*2/4));
            bColorGr4.BackColor=Pal.IntColor(Pal.ColorIntensity765(color,rgbs*1/4));
          }          
        }


    private void bBW_MouseUp(object sender, MouseEventArgs e)
    {
      Clear(true);
    }


    private void bClear_MouseUp(object sender, MouseEventArgs e) {
      Clear(GDI.ShiftKey);
    }


    private void bRadius_MouseUp(object sender, MouseEventArgs e)
    {
      Button b=sender as Button;
      string t=GetTag(sender);
      bool dec=e.Button==MouseButtons.Right,r2=t=="r2",ctrl=GDI.CtrlKey;
      SetRadius(r2&&!dec&&Radius<5?5:Radius+(dec?-1:1)*(r2?ctrl?30:10:ctrl?3:1));            
    }


    private void chColor_CheckedChanged(object sender,EventArgs e) {

    }

    private void Button_MouseUp(object sender, MouseEventArgs e) {
      string tag=GetTag(sender);
      int d=e.Button==MouseButtons.Right?-2:-1;
      if(tag=="comb") SetCombine(d);
      else if(tag=="shape") SetShape(d);
      else SetHill(d);
    }

    private void ColorDiag() {
          CDialog.Color=bColor.BackColor;
          CDialog.FullOpen=true;
          if(DialogResult.OK==CDialog.ShowDialog(this)) {
            bColor.BackColor=CDialog.Color;
            int[] cc=CDialog.CustomColors;
            int h,c=(CDialog.Color.B<<16)|(CDialog.Color.G<<8)|CDialog.Color.R;
            if(cc[0]==c) return;
            for(h=0;h<cc.Length-1;h++)
              if(cc[h]==c) break;
            while(h>0) {
              cc[h]=cc[h-1];
              h--;
            }
            cc[0]=c;  
            CDialog.CustomColors=cc;
          }

        }

        private void bHeight_Click(object sender,EventArgs e) {
        }
        private void Height_MouseUp(object sender, MouseEventArgs e)    {

          Button b=sender as Button;
          bh1.BackColor=bh2.BackColor=bh3.BackColor=bh4.BackColor=bh5.BackColor=SystemColors.Control;
          b.BackColor=Color.DarkGray;
          Height2=byte.Parse(b.Tag as string);
        }
     void RotateColor(byte[] data,int i) {
       byte r=data[i],g=data[i+1],b=data[i+2];
       data[i]=b;data[i+1]=r;data[i+2]=g;
     }
    private unsafe void miMColor_Click(object sender, EventArgs e) {
      var ts=sender as ToolStripItem;
      switch(""+ts.Tag) {
        case "rotate":map.Func(Pal.RotateColor);Repaint(true);break;
        case "rgb2cmy":map.Func(Pal.RGB2CMY);Repaint(true);break;
        case "rxb":map.Func(Pal.RXB);Repaint(true);break;
      }

    }

            private void Radius_MouseUp(object sender, MouseEventArgs e) {
                 SetRadius(int.Parse(GetTag(sender)));
            }
        

        private void panel_MouseUp(object sender,MouseEventArgs e) {
          AnchorStyles anch=panel.Anchor;
          Point l=panel.Location;
          if(e.X<0) {
            l.X=0;
            anch=anch&~AnchorStyles.Right|AnchorStyles.Left;
          } else if(e.X>panel.Width) {
            l.X=ClientSize.Width-panel.Width;
            anch=anch&~AnchorStyles.Left|AnchorStyles.Right;
          }
          if(e.Y<0) {
            l.Y=0;
            anch=anch&~AnchorStyles.Bottom|AnchorStyles.Top;
          } else if(e.Y>panel.Height) {
            l.Y=ClientSize.Height-panel.Height;
            anch=anch&~AnchorStyles.Top|AnchorStyles.Bottom;
          }
          if(l.X!=panel.Left||l.Y!=panel.Top) panel.Location=l;
          if(anch!=panel.Anchor) panel.Anchor=anch;
        }

        private void miWhite_Click(object sender, EventArgs e) { SetWhite(!White,true,true);}
        
        void SetWhite(bool value,bool repaint,bool swap) {
          bool w2=White;
          White=value;
          miHillWhite.Checked=White;
          if(w2!=White) SetColor(Pal.NegColor(Color2));
          bWhite.BackColor=ColorInt(White?0:Pal.White);
          if(map!=null) {
            if(swap) map.Invert(true,false);
            if(repaint) Repaint(true);
          }
          //if(Color2==(White?Pal.White:0)) SetColor(Color2^Pal.White);
          //bWhite.BackColor=ColorInt(value?0:0xffffff);
          //bBlue.BackColor=ColorInt(;
        }

        private void miAutoSnap_Click(object sender, EventArgs e) {
          AutoSnap^=true;
          miAutoSnap.Checked=AutoSnap;
        }

        private void miFile_Click(object sender, EventArgs e) { 
          string tag=""+(sender as ToolStripItem).Tag;
          switch(tag) {
           case "new":NewFile();break;
           case "open":OpenFile();break;
           case "save":SaveFile(false);break;
           case "saveas":SaveFile(true);break;
           case "export":ExportFile(true);break;
           case "page":PrintPage(true);break;
           case "print":Print();break;
           case "exit":Close();break;
          }
        }

    }

    public class UndoItem {
      public hfmap map;
      public int sx,sy,zoom,angle,pix,piy;
    }
    
    public static class GDI {
      public static bool CtrlRKey {get{ return 0!=(0x8000&GDI.GetKeyState(Keys.RControlKey));}}
      public static bool CtrlLKey {get{ return 0!=(0x8000&GDI.GetKeyState(Keys.LControlKey));}}
      public static bool CtrlKey {get{ return 0!=(0x8000&GDI.GetKeyState(Keys.ControlKey));}}
      public static bool ShiftKey {get{ return 0!=(0x8000&GDI.GetKeyState(Keys.ShiftKey));}}
      public static bool ShiftLKey {get{ return 0!=(0x8000&GDI.GetKeyState(Keys.LShiftKey));}}
      public static bool ShiftRKey {get{ return 0!=(0x8000&GDI.GetKeyState(Keys.RShiftKey));}}
      public static bool AltKey { get { return 0 != (0x8000 & GDI.GetKeyState(Keys.Menu)); } }
      public static bool AltLKey { get { return 0 != (0x8000 & GDI.GetKeyState(Keys.LMenu)); } }
      public static bool AltRKey { get { return 0 != (0x8000 & GDI.GetKeyState(Keys.RMenu)); } }
      public static bool CapsLock { get { return 0 != (0x0001 & GDI.GetKeyState(Keys.CapsLock)); } }
      public static bool NumLock { get { return 0 != (0x0001 & GDI.GetKeyState(Keys.NumLock)); } }
      public static bool LButton { get { return 0 != (0x8000 & GDI.GetKeyState(Keys.LButton)); } }
      public static bool RButton { get { return 0 != (0x8000 & GDI.GetKeyState(Keys.RButton)); } }
      public static bool Space { get { return 0 != (0x8000 & GDI.GetKeyState(Keys.Space)); } }
    
     [DllImport("user32"), SuppressUnmanagedCodeSecurity, PreserveSig]
     public static extern short GetKeyState(Keys key);

     [DllImport("gdi32"), SuppressUnmanagedCodeSecurity, PreserveSig]
     public static extern int SetROP2(IntPtr hdc, int fnDrawMode);    
     [DllImport("gdi32") ,SuppressUnmanagedCodeSecurity, PreserveSig]  
     public static extern bool DeleteObject(IntPtr hObject);
     [DllImport("gdi32") ,SuppressUnmanagedCodeSecurity, PreserveSig]  
     public static extern IntPtr CreateSolidBrush( int crColor );
     [DllImport("user32") ,SuppressUnmanagedCodeSecurity, PreserveSig]  
     public static extern int FillRect(IntPtr hdc, ref RECT lprc,IntPtr hbr);
     [StructLayout(LayoutKind.Sequential)]
     public struct RECT { 
       public int Left, Top, Right, Bottom;
       public RECT(int left,int top,int right,int bottom) {
         Left=left;Top=top;Right=right;Bottom=bottom;
       }
     }
    }

}
