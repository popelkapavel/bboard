using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.IO;
using System.IO.Compression;


namespace bboard {
    public enum Combine {None,Max,Min,Mul,Div,Add,Sub};
    public delegate byte f1(byte x);
    public delegate void fc(byte[] data,int i);
    public unsafe delegate void ff(byte* data);

    public class hfmap {
      public int Width,Height;
      public byte[] Data; //r,g,b,h

      public override string ToString() {
        return ""+Width+"x"+Height;
      }
      public static byte div255(int x) {
        return (byte)((x+1+(x>>8))>>8);
      }


      public hfmap() {}
      public hfmap(int width,int height) {Alloc(width,height);}
      public hfmap(hfmap src) {Copy(src);}
      public hfmap Clone() { return new hfmap(this);}
      public void Copy(hfmap src) {
        Data=src.Data.Clone() as byte[];
        Width=src.Width;Height=src.Height;
      }
      public void Alloc(int width,int height) {        
        Data=new byte[4*(Width=width)*(Height=height)];
      }
      
      public void Invert(bool color,bool beta) {
        unsafe{ fixed(byte* pd=Data) {        
        for(int i=0,dl=Data.Length;i<dl;i++) {
          if(color) Pal.NegColor(pd,i);
          i+=3;
          if(beta) pd[i]^=255;
        }
       }}
      }
      public void ZeroReplace(bool white) {
        byte s=(byte)(white?255:0);
        for(int i=0;i<Data.Length;i+=4) 
          if(Data[i+3]==255) {
            if(Data[i]==s&&Data[i+1]==s&&Data[i+2]==s)
              Data[i]=Data[i+1]=Data[i+2]=(byte)(255-s);
          }
      }
      
      public void FuncHeight(f1 f) {
       unsafe{ fixed(byte* pd=Data) { 
        for(int i=0,dl=Data.Length;i<dl;i++) {
          i+=3;
          pd[i]=f(pd[i]);
        }
       }}
      }
      public void Func(fc f) {
        for(int i=0,dl=Data.Length;i<dl;i+=4)
          f(Data,i);
      }
      public unsafe void Func(ff f) {
       fixed(byte* pd=Data) { 
        for(int i=0,dl=Data.Length;i<dl;i+=4)
          f(pd+i);
       }
      }
      /*
      void interpolation_cell(double x,int width,double xi,double xa,out int c,out double r) {
        double x2=(x-xi)/(xa-xi)*(Width-1);
        if(x2<0) {
          c=0;r=0;
        } else if(x2>=Width-1) {
          c=Width-2;r=1;
        } else {
          c=(int)x2;r=x2-c;
        }
      }     
      double interpolation(double x,double y,double ix,double iy,double ax,double ay) {
        int cx,cy;
        double rx,ry;
        interpolation_cell(x,Width,ix,ax,out cx,out rx);
        interpolation_cell(y,Height,iy,ay,out cy,out ry);
        int p=cy*Width+cx;
        return Data[p]*(1-rx)*(1-ry)+Data[p+1]*(rx)*(1-ry)+Data[p+Width]*(1-rx)*ry+Data[p+Width+1]*rx*ry;
       }
       */
       static void LinearZoom(int bpl,byte[] d,int di,int n,int bpl2,byte[] s,int si,int m) {
         int s0,s1,s2,s3,i,x=0,y=n,k,t,q;
         if(n<1||m<1) return;         
         for(i=0;i<n;i++) {
           x=t=m;s0=s1=s2=s3=0;
           while(x>0) {
             k=x<y?x:y;
             x-=k;y-=k;
             q=k*s[si+3];
             if(q>0) {
               s3+=q;
               s0+=q*s[si+0];s1+=q*s[si+1];s2+=q*s[si+2];
              }
             if(y==0) {
               y=n;
               si+=bpl2;
             }
           }          
           if(s3>0) {
             d[di+0]=(byte)(s0/s3);d[di+1]=(byte)(s1/s3);d[di+2]=(byte)(s2/s3);
             d[di+3]=(byte)(s3/m);
           } else
             d[di+0]=d[di+1]=d[di+2]=d[di+3]=0;
           di+=bpl;
         }
       }


      public hfmap Resize(int width,int height) {
        int x,y;
        int p=0;
        var m1=new hfmap(width,Height);
        for(y=0;y<Height;y++) 
          LinearZoom(4,m1.Data,y*4*width,width,4,Data,y*4*Width,Width);
        var m2=new hfmap(width,height);
        for(x=0;x<width;x++) 
          LinearZoom(4*width,m2.Data,x*4,height,4*width,m1.Data,x*4,Height);
        return m2;
      }
      public bool Copy(int dx,int dy,hfmap src,int x0,int y0,int x1,int y1) {
        if(!src.Intersected(ref x0,ref y0,ref x1,ref y1,ref dx,ref dy,Width,Height)) return false;        
        for(;y0<=y1;y0++,dy++) {
          int g=(Width*dy+dx)*4,h=(src.Width*y0+x0)*4;
          Array.Copy(src.Data,h,Data,g,4*(x1-x0+1));
        }
        return true;
      }
      public bool Op(int dx,int dy,hfmap src,int x0,int y0,int x1,int y1,bool min) {
        if(!src.Intersected(ref x0,ref y0,ref x1,ref y1,ref dx,ref dy,Width,Height)) return false;        
        unsafe { fixed(byte* pd=Data,sd=src.Data) {
          for(;y0<=y1;y0++,dy++) {
            byte* g=pd+(Width*dy+dx)*4,h=sd+(src.Width*y0+x0)*4;
            for(int x=x0;x<=x1;x++,g+=4,h+=4) {
              byte gh=g[3],hh=h[3];
              if(min) {
                hh=(byte)(255-hh);if(hh==255||(gh<=hh)) continue;
              } else {
                if(gh>=hh) continue;
              }
              g[0]=h[0];g[1]=h[1];g[2]=h[2];
              g[3]=hh;
            }
          }
        }}
        return true;
      }
      public hfmap Extent(int x,int y) {
        if(x>=0&&x<Width&&y>=0&&y<Height) return null;
        int nw=Width+(x<0?-x:x<Width?0:x-Width+1),nh=Height+(y<0?-y:y<Height?0:y-Height+1);
        var m2=new hfmap(nw,nh);
        m2.Copy(x<0?-x:0,y<0?-y:0,this,0,0,Width,Height);
        return m2;
      }
      public void Zero(int color) {
        byte c0=(byte)(color&255),c1=(byte)((color>>8)&255),c2=(byte)((color>>16)&255);
        for(int g=3,ge=Width*Height*4;g<ge;g+=4)
          if(Data[g]==0) {
            Data[g-3]=c0;
            Data[g-2]=c1;
            Data[g-1]=c2;
          }
      }
      public byte SaveType(out int color) {
        byte r=0;
        byte c0=0,c1=0,c2=0,d0,d1,d2;
        bool gray=false;
        for(int i=0,e=4*Width*Height;i<e;i+=4) {
          if(Data[i+3]==0) continue;
          d0=Data[i];d1=Data[i+1];d2=Data[i+2];
          if(r==0) { 
            c0=d0;c1=d1;c2=d2;
            r=1;
            gray=c0==c1&&c0==c2;
            continue;
          }
          if(r==1) {
            if(d0==c0&&d1==c1&&d2==c2) continue;
            r=2;
          }          
          if(gray&&d0==d1&&d0==d2) continue;
          r=3;
          break;
        }
        color=r==1?c0|(c1<<8)|(c2<<16):0;
        return r;
      }
      public void Save(string filename,bool white) {
        BinaryWriter bw=new BinaryWriter(new FileStream(filename,FileMode.OpenOrCreate,FileAccess.Write));
        bw.Write((ushort)0x6262);
        bw.Write((ushort)Width);
        bw.Write((ushort)Height);
        bw.Write(white);
        int c;
        byte st=SaveType(out c);
        bw.Write(st);
        if(st<1) {
          bw.Close();
          return;
        }
        if(st==1) {
          bw.Write((byte)(c&255));
          bw.Write((byte)((c>>8)&255));
          bw.Write((byte)((c>>16)&255));
        } else
          Zero(0);
        bw.Flush();
        GZipStream gz=new GZipStream(bw.BaseStream,CompressionMode.Compress);
        //gz.Write(Data,0,Data.Length);
        byte[] data=new byte[Width];
        for(int p=0;p<Data.Length;p+=4*Width) {
          int n,i,d;
          byte b,b2;
          for(i=d=n=b=0;d<Width;i+=4) {
            b2=Data[p+i+3];
            data[d++]=(byte)(b2-b+128);
            b=b2;
            if(b2!=0) n++;
          }
          gz.Write(data,0,Width);
          if(st>1&&n>0) for(int j=st==2?2:0;j<3;j++) {
            for(i=d=b=0;d<n;i+=4) {
              if(Data[p+i+3]==0) continue;
              b2=Data[p+i+j];
              data[d++]=(byte)(b2-b+128);
              b=b2;              
            }
            gz.Write(data,0,n);
          }
        }
        gz.Flush();
        gz.BaseStream.SetLength(gz.BaseStream.Position);
        gz.Close();        
      }
      public static hfmap Load(hfmap map,string filename,out bool white) {
        white=false;
        if(!File.Exists(filename)) return null;
        using(BinaryReader br=new BinaryReader(new FileStream(filename,FileMode.Open,FileAccess.Read))) {
          uint bb=br.ReadUInt16();
          if(bb!=0x6262) return null;
          if(map==null) map=new hfmap();
          map.Width=br.ReadUInt16();
          map.Height=br.ReadUInt16();
          white=br.ReadBoolean();
          byte st=br.ReadByte(),c0,c1,c2;
          //white=br.ReadBoolean();
          map.Alloc(map.Width,map.Height);
          if(st>0) { 
            if(st==1) {
              c0=br.ReadByte();
              c1=br.ReadByte();
              c2=br.ReadByte();
            } else c0=c1=c2=0;
            using(GZipStream gz=new GZipStream(br.BaseStream,CompressionMode.Decompress,true)) {
             byte[] data=new byte[map.Width];
             for(int p=0;p<map.Data.Length;p+=4*map.Width) {
               int i,d,n;
               byte b2,b;
               int r=gz.Read(data,0,data.Length);
               for(d=i=n=b=0;d<map.Width;i+=4) {
                 b2=data[d++];
                 map.Data[p+i+3]=b=(byte)(b+b2-128);
                 if(b!=0) if(st==1) {
                     map.Data[p+i+0]=c0;
                     map.Data[p+i+1]=c1;
                     map.Data[p+i+2]=c2;
                   } else
                     n++;
               }
               if(st>1&&n>0) for(int j=st==2?2:0;j<3;j++) {                 
                 r=gz.Read(data,0,n);  
                 for(d=i=b=0;d<n;i+=4) {
                   while(map.Data[p+i+3]==0) i+=4;
                   b2=data[d++];
                   map.Data[p+i+j]=b=(byte)(b+b2-128);
                   if(st==2)
                     map.Data[p+i]=map.Data[p+i+1]=b;
                 }
               }
             }
           }
          }
        }
        return map;
      }
      public void ExportGrayPng(string filename,bool inv) {
        byte xor=(byte)(inv?255:0);
        using(Bitmap bm=new Bitmap(Width,Height,PixelFormat.Format8bppIndexed)) {
          var p=bm.Palette;
          for(int i=0;i<256;i++) p.Entries[i]=System.Drawing.Color.FromArgb(i,i,i);
          bm.Palette=p;
          BitmapData bd=bm.LockBits(new Rectangle(0,0,Width,Height),ImageLockMode.WriteOnly,PixelFormat.Format8bppIndexed);
          byte[] line=new byte[Width];
          for(int y=0;y<Height;y++) {
            for(int h=y*4*Width+3,x=0;x<Width;h+=4)
              line[x++]=(byte)(Data[h]^xor);
            Marshal.Copy(line,0,new IntPtr(bd.Scan0.ToInt64()+bd.Stride*y),Width);
          }
          bm.UnlockBits(bd);
          bm.Save(filename,ImageFormat.Png);
        }
      }
      public void ExportPgm(string filename,bool inv) {
        byte xor=(byte)(inv?255:0);
        using(var s=new FileStream(filename,FileMode.OpenOrCreate,FileAccess.Write,FileShare.Read)) {
          string hdr="P5\n"+Width+" "+Height+"\n255\n";
          s.Write(System.Text.Encoding.ASCII.GetBytes(hdr),0,hdr.Length);
          byte[] line=new byte[Width];
          for(int y=0;y<Height;y++) {
            for(int h=y*4*Width+3,x=0;x<Width;h+=4)
              line[x++]=((byte)(Data[h]^xor));
            s.Write(line,0,Width);
          }
          s.SetLength(s.Position);
        }
      }
      public void ExportPng(string filename,bool alpha,bool white) {
        PixelFormat pf=alpha?PixelFormat.Format32bppArgb:PixelFormat.Format24bppRgb;
        Bitmap bm=new Bitmap(Width,Height,pf);
        BitmapData bd=bm.LockBits(new Rectangle(0,0,Width,Height),ImageLockMode.WriteOnly,pf);
        byte[] line=alpha?null:new byte[3*Width];
        for(int i=0;i<Height;i++) {
          IntPtr dst=new IntPtr(bd.Scan0.ToInt64()+bd.Stride*i);
          if(alpha)
            Marshal.Copy(Data,i*4*Width,dst,4*Width);
          else {
            for(int g=0,ge=3*Width,h=i*4*Width;g<ge;g+=3,h+=4) {
              byte c0=Data[h],c1=Data[h+1],c2=Data[h+2],d=Data[h+3];
              if(white) {
                c0=(byte)(255-(d*(255-c0))/255);
                c1=(byte)(255-(d*(255-c1))/255);
                c2=(byte)(255-(d*(255-c2))/255);
              } else {
                c0=(byte)(d*c0/255);
                c1=(byte)(d*c1/255);
                c2=(byte)(d*c2/255);
              }
              line[g]=c0;line[g+1]=c1;line[g+2]=c2;
            }
            Marshal.Copy(line,0,dst,3*Width);
          }
        }
        bm.UnlockBits(bd);
        bm.Save(filename);        
      }

      public void MinMax(out byte min,out byte max) {
        min=max=Data[0];
        for(int i=1;i<Data.Length;i++)
          if(Data[i]<min) min=Data[i];
          else if(Data[i]>max) max=Data[i];
      }
      public static int Color(byte[] data,int idx) { 
        unsafe { fixed(byte* pd=data) {
          byte *p=pd+idx;
          return p[0]|(p[1]<<8)|(p[2]<<16);
        }}
      }
      public static void Color(byte[] data,int idx,int color) {
       unsafe { fixed(byte* pd=data) {
        byte *p=pd+idx;
        *p++=(byte)(color);
        *p++=(byte)(color>>8);
        *p=(byte)(color>>16);
       }}
      }      
      public static void Color(byte[] data,int idx,int color,byte h) {
       unsafe { fixed(byte* pd=data) {
        byte *p=pd+idx;
        *p++=(byte)(color);
        *p++=(byte)(color>>8);
        *p++=(byte)(color>>16);
        *p=h;
       }}
      }      
      
      public static void Color(int c,out byte r,out byte g,out byte b) {
        r=(byte)(c&255);
        g=(byte)((c>>8)&255);
        b=(byte)((c>>16)&255);
      }
      public void Clear(byte value,int color,bool conly) {
        Clear(value,color,conly,0,Data.Length);
      }
      public void Clear(byte value,int color,bool conly,int begin,int end) {
        byte r,g,b;
        Color(color,out r,out g,out b);
        unsafe { fixed(byte *pd=Data) {
          byte *p=pd+begin; 
          for(int i=begin;i<end;i+=4,p++) {          
          *p++=r;*p++=g;*p++=b;
          if(!conly) *p=value;
        }
        }}
      }

      public void HLine(int x0,int x1,int y,byte value,int color,bool conly) {
        if(y<0||y>=Height) return;
        if(x0>x1) { int r=x0;x0=x1;x1=r;}
        if(x1<0||x0>=Width) return;
        if(x0<0) x0=0;if(x1>=Width) x1=Width-1;
        Clear(value,color,conly,4*(y*Width+x0),4*(y*Width+x1+1));
      }
      public void Trapez(int x00,int x10,int y0,int x01,int x11,int y1,byte value,int color,bool conly) {
        if(x00>x10) { int r=x00;x00=x10;x10=r;}
        if(x01>x11) { int r=x01;x01=x11;x11=r;}
        if(y0==y1) {          
          HLine(x00<x10?x00:x10,x10>x11?x10:x11,y0,value,color,conly);
          return;
        }
        if(y0>y1) { 
          int r=y0;y0=y1;y1=r;
          r=x00;x01=x00;x01=r;r=x10;x11=x10;x11=r;          
        }
        for(int y=y0,a=y1-y0,b=0,c=a;y<=y1;y++,a--,b++)
          HLine((a*x00+b*x01)/c,(a*x10+b*x11)/c,y,value,color,conly);
      }
      public void Triangle(int x0,int y0,int x1,int y1,int x2,int y2,byte value,int color,bool conly) {
        if(y1<y0) { int r=x0;x0=x1;x1=r;r=y0;y0=y1;y1=r;}
        if(y2<y0) { int r=x0;x0=x2;x2=r;r=y0;y0=y2;y2=r;}
        if(y2<y1) { int r=x1;x1=x2;x2=r;r=y1;y1=y2;y2=r;}
        if(y2<0||y0>=Height) return;
        int x=y1==y0?x0:y1==y2?x2:((y2-y1)*x0+(y1-y0)*x2)/(y2-y0);
        Trapez(x0,x0,y0,x,x1,y1,value,color,conly);
        Trapez(x,x1,y1,x2,x2,y2,value,color,conly);
      }
      public void Rectangle(int x0,int y0,int x1,int y1,int x2,int y2,byte value,int color,bool conly) {
        Triangle(x0,y0,x1,y1,x2,y2,value,color,conly);
        int x3=x2-x1+x0,y3=y2-y1+y0;
        Triangle(x0,y0,x2,y2,x3,y3,value,color,conly);
      }

      public static void Sort(ref int x0,ref int x1) {
        if(x0<=x1) return;
        int x;
        x=x0;x0=x1;x1=x;
      }
      public static void Sort(ref int x0,ref int y0,ref int x1,ref int y1) { Sort(ref x0,ref x1);Sort(ref y0,ref y1);}
      public static void MinMax(ref int x0,ref int x1,int x2,int x3) {
        int x;
        if(x0>x2) {x=x0;x0=x2;x2=x;}
        if(x3>x1) {x=x1;x1=x3;x3=x;}
        if(x0>x3) x0=x3;
        if(x2>x1) x1=x2;
      }
      public static void MinMax(ref int x0,ref int y0,ref int x1,ref int y1,int x2,int y2,int x3,int y3) { 
        MinMax(ref x0,ref x1,x2,x3);MinMax(ref y0,ref y1,y2,y3);
      }
      public bool Intersected(ref int x0,ref int y0,ref int x1,ref int y1) {
        Sort(ref x0,ref x1);Sort(ref y0,ref y1);
        if(x1<0||x0>=Width||y1<0||y0>=Height) return false;
        if(x0<0) x0=0;if(x1>=Width) x1=Width-1;
        if(y0<0) y0=0;if(y1>=Height) y1=Height-1;
        return true;
      }
      public bool Intersected(ref int x0,ref int y0,ref int x1,ref int y1,ref int sx,ref int sy,int width,int height) {
        Sort(ref x0,ref x1);Sort(ref y0,ref y1);
        if(x1<0||x0-(sx<0?sx:0)>=Width||y1<0||y0-(sy<0?sy:0)>=Height) return false;
        if(x0<0) {sx-=x0;x0=0;} if(x1>=Width) x1=Width-1;
        if(y0<0) {sy-=y0;y0=0;} if(y1>=Height) y1=Height-1;
        if(sx<0) {x0-=sx;sx=0;}
        if(sy<0) {y0-=sy;sy=0;}
        int i;
        if((i=sx+x1-x0-width+1)>0) x1-=i;
        if((i=sy+y1-y0-height+1)>0) y1-=i;
        if(y1<y0||x1<x0) return false;
        return true;
      }
      public void Clear(int x0,int y0,int x1,int y1,byte value,int color,bool conly) {
        if(!Intersected(ref x0,ref y0,ref x1,ref y1)) return;
        byte r,g,b;
        Color(color,out r,out g,out b);
       unsafe { fixed(byte* pd=Data) {       
        for(;y0<=y1;y0++)
          for(int i=(y0*Width+x0)*4,ie=i+(x1-x0+1)*4;i<ie;i++) {
            pd[i++]=r;pd[i++]=g;pd[i++]=b;
            if(!conly) pd[i]=value;
          }
        }}
      }
      public static int Dist(int dx,int dy) {
        int d2=dx*dx+dy*dy;
        return d2<16?d2<4?d2<1?0:1:d2<9?2:3:(int)(Math.Sqrt(dx*dx+dy*dy));
      }
      public static int ColorMix(int a,int b,int i,int d) {
        if(a==b||i<0) return a;        
        if(i>=d) {
          if(i==0&&d==0) { i=1;d=2;} else return b;
        }
        int j=d-i,c0=(j*(a&255)+i*(b&255))/d,c1=(j*((a>>8)&255)+i*((b>>8)&255))/d,c2=(j*((a>>16)&255)+i*((b>>16)&255))/d;
        return c0|(c1<<8)|(c2<<16);
      }
      public void Color4(int x0,int y0,int x1,int y1,int c00,int c01,int c10,int c11) { 
        if(x0>x1) { int r=x0;x0=x1;x1=r;r=c00;c00=c10;c10=r;r=c01;c01=c11;c11=r;}
        if(y0>y1) { int r=y0;y0=y1;y1=r;r=c00;c00=c01;c01=r;r=c10;c10=c11;c11=r;}
        int xx0=x0,yy0=y0,xx1=x1,yy1=y1,c;
        if(!Intersected(ref x0,ref y0,ref x1,ref y1)) return;
        if(xx0!=x0||xx1!=x1) {          
          c00=ColorMix(c=c00,c10,x0-xx0,xx1-xx0);
          c10=ColorMix(c,c10,x1-xx0,xx1-xx0);
          c01=ColorMix(c=c01,c11,x0-xx0,xx1-xx0);
          c11=ColorMix(c,c11,x1-xx0,xx1-xx0);
        }
        if(yy0!=y0||yy1!=y1) {
          c00=ColorMix(c=c00,c01,y0-yy0,yy1-yy0);
          c01=ColorMix(c,c01,y1-yy0,yy1-yy0);
          c10=ColorMix(c=c10,c11,y0-yy0,yy1-yy0);
          c11=ColorMix(c,c11,y1-yy0,yy1-yy0);
        }
        xx0=x0;yy0=y0;xx1=x1;yy1=y1;
       unsafe { fixed(byte* pd=Data) {       
        for(;y0<=y1;y0++) {
          int c0=ColorMix(c00,c01,y0-yy0,y1-yy0),c1=ColorMix(c10,c11,y0-yy0,y1-yy0);
          for(int i=(y0*Width+x0)*4,ie=i+(x1-x0+1)*4,j=0;i<ie;i++,j++) {
            c=ColorMix(c0,c1,j,x1-x0);
            pd[i++]=(byte)(c&255);pd[i++]=(byte)((c>>8)&255);pd[i++]=(byte)((c>>16)&255);
          }
        }
        }}
      }
      public void GHLine(int x0,int x1,int y,int c0,int c1) {
        if(y<0||y>=Height) return;
        if(x0>x1) { int r=x0;x0=x1;x1=r;r=c0;c0=c1;c1=r;}
        if(x1<0||x0>=Width) return;
        int xx0=x0,xx1=x1,c;
        if(x0<0) x0=0;if(x1>=Width) x1=Width-1;
        if(xx0!=x0||xx1!=x0) {
          c0=ColorMix(c=c0,c1,x0-xx0,xx1-xx0);
          c1=ColorMix(c,c1,x1-xx0,xx1-xx0);          
        }
       unsafe { fixed(byte* pd=Data) {
        for(int i=(y*Width+x0)*4,ie=i+(x1-x0+1)*4,j=0;i<ie;i++,j++) {
          c=ColorMix(c0,c1,j,x1-x0);
          pd[i++]=(byte)(c&255);pd[i++]=(byte)((c>>8)&255);pd[i++]=(byte)((c>>16)&255);
        }
        }}
      }
      public void GTrapez(int x00,int x10,int y0,int x01,int x11,int y1,int c00,int c10,int c01,int c11) {
        if(x00>x10) { int r=x00;x00=x10;x10=r;r=c00;c00=c10;c10=r;}
        if(x01>x11) { int r=x01;x01=x11;x11=r;r=c01;c01=c11;c11=r;}
        if(y0==y1) {          
          GHLine(x00<x10?x00:x10,x10>x11?x10:x11,y0,x00<x10?c00:c10,x10>x11?c10:c11);
          return;
        }
        if(y0>y1) { 
          int r=y0;y0=y1;y1=r;
          r=x00;x01=x00;x01=r;r=x10;x11=x10;x11=r;
          r=c00;c01=c00;c01=r;r=c10;c11=c10;c11=r;
        }
        for(int y=y0,a=y1-y0,b=0,c=a;y<=y1;y++,a--,b++)
          GHLine((a*x00+b*x01)/c,(a*x10+b*x11)/c,y,ColorMix(c00,c01,b,c),ColorMix(c10,c11,b,c));
      }
      public void GTriangle(int x0,int y0,int x1,int y1,int x2,int y2,int c0,int c1,int c2) {
        if(y1<y0) { int r=x0;x0=x1;x1=r;r=y0;y0=y1;y1=r;r=c0;c0=c1;c1=r;}
        if(y2<y0) { int r=x0;x0=x2;x2=r;r=y0;y0=y2;y2=r;r=c0;c0=c2;c2=r;}
        if(y2<y1) { int r=x1;x1=x2;x2=r;r=y1;y1=y2;y2=r;r=c1;c1=c2;c2=r;}
        if(y2<0||y0>=Height) return;
        int x=y1==y0?x0:y1==y2?x2:((y2-y1)*x0+(y1-y0)*x2)/(y2-y0),c=ColorMix(c0,c2,y1-y0,y2-y0);
        GTrapez(x0,x0,y0,x,x1,y1,c0,c0,c,c1);
        GTrapez(x,x1,y1,x2,x2,y2,c,c1,c2,c2);
      }
      public void GRectangle(int x0,int y0,int x1,int y1,int x2,int y2,int c0,int c1,int c2,int c3) {
        GTriangle(x0,y0,x1,y1,x2,y2,c0,c1,c2);
        int x3=x2-x1+x0,y3=y2-y1+y0;
        GTriangle(x0,y0,x2,y2,x3,y3,c0,c2,c3);
      }
      public bool IsEmpty(int x0,int y0,int x1,int y1) {
        if(!Intersected(ref x0,ref y0,ref x1,ref y1)) return true;
       unsafe { fixed(byte* pd=Data) {       
        for(;y0<=y1;y0++)
          for(int i=(y0*Width+x0)*4+3,ie=i+(x1-x0+1)*4;i<ie;i+=4)
            if(pd[i]!=0) return false;
        }}
        return true;
      }
      public bool Bounding(ref int x0,ref int y0,ref int x1,ref int y1) {
        if(!Intersected(ref x0,ref y0,ref x1,ref y1)) return false;
        int xi=x0,xa=x1;
        for(;y0<=y1;y0++) if(!IsEmpty(x0,y0,x1,y0)) goto l1;
        return false;
       l1:
        for(;y0<y1;y1--) if(!IsEmpty(x0,y1,x1,y1)) break;
        for(;x0<x1;x0++) if(!IsEmpty(x0,y0,x0,y1)) break;  
        for(;x0<x1;x1--) if(!IsEmpty(x1,y0,x1,y1)) break;  
        return true;
      }
      public static double Shape(double[] shape,double x) {
        if(x<=0) return shape[0];
        int n=shape.Length-1;
        if(x>=1) return shape[n];
        double xn=x*n;
        int i=(int)xn;
        double t=(xn-i)/n;
        return shape[i]*(1-t)+shape[i]*t;
      }
      public void FuncRadial(double x,double y,double r,int h,byte[] draw,double[] shape) {
        if(x+r<0||x-r>=Width||y+r<0||y-r>=Height) return;
        int xi=(int)Math.Ceiling(x-r),yi=(int)Math.Ceiling(y-r),xa=(int)Math.Floor(x+r),ya=(int)Math.Floor(y+r);
        if(xi<0) xi=0;if(yi<0) yi=0;
        if(xa>=Width-1) xa=Width-1;if(ya>=Height-1) ya=Height-1;
       unsafe { fixed(byte* pd=draw) {
        for(int yy=yi;yy<=ya;yy++) {
          double ry=yy-y;
          for(int xx=xi;xx<=xa;xx++) {
            double rx=xx-x;
            double r2=ry*ry+rx*rx;
            if(r2<=r*r) {
              double rr=Math.Sqrt(r2);//hh=h*bz.fx((r-rr)/r);
              byte hh;
              if(shape!=null) hh=(byte)(h*Shape(shape,(r-rr)/r));
              else hh=(byte)((r-rr)*h/r);              
/*              
              switch(func) {
               //case Function.Cone:rr=Math.Sqrt(r2);hh=h*(r-rr)/r;break;
               //case Function.Sinus:hh=h*(r-rr)/r;break;
               case Function.Cylinder:hh=h;break;
               case Function.Sphere:rr=Math.Sqrt(r2);hh=h*Math.Sqrt(r*r-r2)/r;break;               
               default:rr=Math.Sqrt(r2);hh=h*(r-rr)/r;break;
              }*/
              int idx=Width*yy+xx;                  
              if(pd[idx]<hh) pd[idx]=hh;
            }
          }
        }
        }}
      }
      public void FuncSquare(double x,double y,double r,int h,byte[] draw,double[] shape) {
        if(x+r<0||x-r>=Width||y+r<0||y-r>=Height) return;
        int xi=(int)Math.Ceiling(x-r),yi=(int)Math.Ceiling(y-r),xa=(int)Math.Floor(x+r),ya=(int)Math.Floor(y+r);
        if(xi<0) xi=0;if(yi<0) yi=0;
        if(xa>=Width-1) xa=Width-1;if(ya>=Height-1) ya=Height-1;
       unsafe { fixed(byte* pd=draw) {
        for(int yy=yi;yy<=ya;yy++) {
          double ry=Math.Abs(yy-y);
          for(int xx=xi;xx<=xa;xx++) {
            double rx=Math.Abs(xx-x);
            double rr=rx>ry?rx:ry;
            byte hh;
            if(shape!=null) hh=(byte)(h*Shape(shape,(r-rr)/r));
            else hh=(byte)((r-rr)*h/r);              
            int idx=Width*yy+xx;                  
            if(pd[idx]<hh) pd[idx]=hh;
          }
        }
        }}
      }
      public void FuncSquareLine(int x,int y,int x2,int y2,int r,byte h,byte[] draw,double[] shape) {
        if((x+r<0&&x2+r<0)||(x-r>=Width&&x2-r>=Width)||(y+r<0&&y2+r<0)||(y-r>=Height&&y2-r>=Height)) return;
        if(x2==x&&y2==y) {
          FuncSquare(x,y,r,h,draw,shape);
          return;
        }
        FuncSquare(x,y,r,h,draw,shape);
        FuncSquare(x2,y2,r,h,draw,shape);
        int i,xi=x-r,yi=y-r,xa=x+r,ya=y+r;
        if(xi>(i=x2-r)) xi=i;if(yi>(i=y2-r)) yi=i;
        if(xi<0) xi=0;if(yi<0) yi=0;
        if(xa<(i=x2+r)) xa=i;if(ya<(i=y2+r)) ya=i;
        if(xa>=Width-1) xa=Width-1;if(ya>=Height-1) ya=Height-1;

        int dp=y2-y+x2-x,dm=x2-x-y2+y,d;
        bool xy=Math.Abs(dp)>Math.Abs(dm);        
        for(int yy=yi;yy<=ya;yy++) {          
          for(int xx=xi;xx<=xa;xx++) {
            double r0,rl;
            int rp=yy-y+xx-x,rm=xx-x-yy+y;
            if(xy) {
              if(dp>0&&(rp<0||rp>dp)||dp<0&&(rp>0||rp<dp)) continue;              
              r0=1.0*rp*dm/dp;
              rl=r-Math.Abs(rm-r0)/2;
            } else {
              if(dm>0&&(rm<0||rm>dm)||dm<0&&(rm>0||rm<dm)) continue;
              r0=1.0*rm*dp/dm;
              rl=r-Math.Abs(rp-r0)/2;
            }
            if(rl<0) continue;
            byte hh;
            if(shape!=null) hh=(byte)(h*Shape(shape,rl/r));
            else hh=(byte)(rl*h/r);
            int idx=Width*yy+xx;
            if(draw[idx]<hh) draw[idx]=hh;
          }
        }
        /*
        int n=Math.Max(Math.Abs(x-x2),Math.Abs(y-y2));
        for(i=0;i<n;i++)
           FuncSquare(x+(x2-x)*i/n,y+(y2-y)*i/n,r,h,draw,shape);
        */
      }
      public void FuncDiamond(double x,double y,double r,int h,byte[] draw,double[] shape) {
        if(x+r<0||x-r>=Width||y+r<0||y-r>=Height) return;        
        int xi=(int)Math.Ceiling(x-r),yi=(int)Math.Ceiling(y-r),xa=(int)Math.Floor(x+r),ya=(int)Math.Floor(y+r);
        if(xi<0) xi=0;if(yi<0) yi=0;
        if(xa>=Width-1) xa=Width-1;if(ya>=Height-1) ya=Height-1;
       unsafe { fixed(byte* pd=draw) {
        for(int yy=yi;yy<=ya;yy++) {
          double ry=Math.Abs(yy-y);
          for(int xx=xi;xx<=xa;xx++) {
            double rx=Math.Abs(xx-x);
            double rr=rx+ry;
            if(rr>r) continue;
            byte hh;
            if(shape!=null) hh=(byte)(h*Shape(shape,(r-rr)/r));
            else hh=(byte)((r-rr)*h/r);              
            int idx=Width*yy+xx;                  
            if(pd[idx]<hh) pd[idx]=hh;
          }
        }
        }}
      }
      public void FuncDiamondLine(int x,int y,int x2,int y2,int r,byte h,byte[] draw,double[] shape) {
        if((x+r<0&&x2+r<0)||(x-r>=Width&&x2-r>=Width)||(y+r<0&&y2+r<0)||(y-r>=Height&&y2-r>=Height)) return;
        if(x2==x&&y2==y) {
          FuncDiamond(x,y,r,h,draw,shape);
          return;
        }
        FuncDiamond(x,y,r,h,draw,shape);
        FuncDiamond(x2,y2,r,h,draw,shape);
        int i,xi=x-r,yi=y-r,xa=x+r,ya=y+r;
        if(xi>(i=x2-r)) xi=i;if(yi>(i=y2-r)) yi=i;
        if(xi<0) xi=0;if(yi<0) yi=0;
        if(xa<(i=x2+r)) xa=i;if(ya<(i=y2+r)) ya=i;
        if(xa>=Width-1) xa=Width-1;if(ya>=Height-1) ya=Height-1;
        int dy=y2-y,dx=x2-x;
        bool xy=Math.Abs(dy)>Math.Abs(dx);
        for(int yy=yi;yy<=ya;yy++) {          
          if(xy&&(yy<y&&yy<y2||yy>y&&yy>y2)) continue;
          for(int xx=xi;xx<=xa;xx++) {
            double r0,rl;
            if(xy) {              
              r0=x+1.0*(yy-y)*dx/dy;
              rl=r-Math.Abs(xx-r0);
            } else {
              if(xx<x&&xx<x2||xx>x&&xx>x2) continue;
              r0=y+1.0*(xx-x)*dy/dx;
              rl=r-Math.Abs(yy-r0);
            }
            if(rl<0) continue;
            byte hh;
            if(shape!=null) hh=(byte)(h*Shape(shape,rl/r));
            else hh=(byte)(rl*h/r);
            int idx=Width*yy+xx;
            if(draw[idx]<hh) draw[idx]=hh;
          }
        }
        /*
        int n=Math.Max(Math.Abs(x-x2),Math.Abs(y-y2));
        for(i=0;i<n;i++)
           FuncDiamond(x+(x2-x)*i/n,y+(y2-y)*i/n,r,h,draw,shape);
        */
      }
      public void FuncLine(int x,int y,int x2,int y2,int r,byte h,byte[] draw,double[] shape) {
        if((x+r<0&&x2+r<0)||(x-r>=Width&&x2-r>=Width)||(y+r<0&&y2+r<0)||(y-r>=Height&&y2-r>=Height)) return;
        if(x2==x&&y2==y) {
          FuncRadial(x,y,r,h,draw,shape);
          return;
        }
        int i,xi=x-r,yi=y-r,xa=x+r,ya=y+r;
        if(xi>(i=x2-r)) xi=i;if(yi>(i=y2-r)) yi=i;
        if(xi<0) xi=0;if(yi<0) yi=0;
        if(xa<(i=x2+r)) xa=i;if(ya<(i=y2+r)) ya=i;
        if(xa>=Width-1) xa=Width-1;if(ya>=Height-1) ya=Height-1;
        double a=y2-y,b=x-x2,c,ci,ca,d;
        d=Math.Sqrt(a*a+b*b);
        a/=d;b/=d;
        ci=b*x-a*y;ca=b*x2-a*y2;
        if(ci>ca) {c=ca;ca=ci;ci=c;}
        c=-x*a-y*b;
        for(int yy=yi;yy<=ya;yy++) {
          int ry=yy;
          for(int xx=xi;xx<=xa;xx++) {
            double rx=xx;
            double rl=-1,rr=-1,r2=-1;
            d=b*rx-a*ry;
            if(d>=ci&&d<=ca) {
              d=a*rx+b*ry+c;
              rl=r+(d<0?d:-d);
            }
            if(rl<0) {
              rr=(ry-y)*(ry-y)+(rx-x)*(rx-x);
              r2=(ry-y2)*(ry-y2)+(rx-x2)*(rx-x2);
              if(r2<rr) rr=r2;
              if(rr<=r*r)
                rl=r-Math.Sqrt(rr);
            }
            if(rl<0) continue;
            byte hh;
            if(shape!=null) hh=(byte)(h*Shape(shape,rl/r));
            else hh=(byte)(rl*h/r);
            int idx=Width*yy+xx;
            if(draw[idx]<hh) draw[idx]=hh;
          }
        }
      }
      internal static int Mix(byte h,int c,byte h2,int c2) {
        if(h2==0) return c;
        else if(h==0) return c2;
        int x=h*h+1,y=h2*h2+1;
        int r=x*(c&255)+y*(c2&255);
        int g=x*((c>>8)&255)+y*((c2>>8)&255);
        int b=x*((c>>16)&255)+y*((c2>>16)&255);
        int xy=x+y;
        int rb=r/xy;
        int gb=g/xy;
        int bb=b/xy;
        return rb|(gb<<8)|(bb<<16); 
      }
      internal void ApplyDraw(int color,byte height2,Combine comb,byte[] draw,bool clear,bool white,int minLevel) {
        int j=0;
       unsafe { fixed(byte* pd=Data,dd=draw) {
        for(int i=0,dl=draw.Length;i<dl;i++)
          if(dd[i]>0) {
            byte di=dd[i],di2=pd[j+3];
            int color2=pd[j]|(pd[j+1]<<8)|(pd[j+2]<<16);
            if(comb==Combine.None) {
              if(white) color2=Mix(di,color,128,color2);
              else color2=Mix(di,color,224,color2);
            } else 
              color2=Mix(di,color,di2,color2);
            pd[j]=(byte)(color2&255);
            pd[j+1]=(byte)(color2>>8);
            pd[j+2]=(byte)(color2>>16);
            j+=3;
            if(comb==Combine.Max) {
              if(di>pd[j]) pd[j]=di;
            } else if(comb==Combine.Min) {
              di=(byte)(255-di);
              if(minLevel>0)
                if(di<minLevel) di=0;else di=(byte)((di-minLevel)*255/(255-minLevel));
              if(di<pd[j]) {
                //Color[i]=Mix(di,color,Data[i],Color[i]);
                pd[j]=di;
              }
            } else if(comb==Combine.Mul) {
              if(pd[j]<255) pd[j]+=(byte)(((255-pd[j])*di)/255/2);
            } else if(comb==Combine.Div) {
              if(pd[j]>0) pd[j]-=(byte)((pd[j]*di)/255/2);
            } else if(comb==Combine.Add) {
              int r=pd[j]+di/4;
              pd[j]=(byte)(r<255?r:255);
            } else if(comb==Combine.Sub) {
              int r=pd[j]-di/4;
              pd[j]=(byte)(r>0?r:0);
            }
            j++;
            if(clear) dd[i]=0;
          } else
            j+=4;
        }}
      }
      bool FloodFill(byte h,byte hl,bool dir) {
        return dir?h<hl:h>hl;
      }
      public void FloodFill(int x,int y,byte h,int color) {
        if(x<0||x>=Width||y<0||y>=Height) return;
        byte hf=h,h2=Data[4*(y*Width+x)+3];
        bool dir;
        if(hf>h2) dir=true;else if(hf<h2) dir=false;else return;
        hfmap.Color(Data,4*(y*Width+x),color,hf);
        int n=1,nn=0;
        int[,] xy,nxy,xy1=new int[2*(Width+Height),2],xy2=new int[2*(Width+Height),2];
        xy1[0,0]=x;xy1[0,1]=y;
        xy=xy1;nxy=xy2;
        while(n>0) {
          nn=0;
          for(int i=0;i<n;i++) {
            x=xy[i,0];y=xy[i,1];
            int p=4*(y*Width+x);
            if(x>0&&FloodFill(Data[p-4+3],hf,dir)) {
              hfmap.Color(Data,p-4,color,hf);
              nxy[nn,0]=x-1;nxy[nn++,1]=y;
            }
            if(y>0&&FloodFill(Data[p-4*Width+3],hf,dir)) {
              hfmap.Color(Data,p-4*Width,color,hf);
              nxy[nn,0]=x;nxy[nn++,1]=y-1;
            }
            if(x<Width-1&&FloodFill(Data[p+4+3],hf,dir)) {
              hfmap.Color(Data,p+4,color,hf);
              nxy[nn,0]=x+1;nxy[nn++,1]=y;
            }
            if(y<Height-1&&FloodFill(Data[p+4*Width+3],hf,dir)) {
              hfmap.Color(Data,p+4*Width,color,hf);
              nxy[nn,0]=x;nxy[nn++,1]=y+1;
            }
          }
          n=nn;
          xy=nxy;
          nxy=xy==xy2?xy1:xy2;
        }  
      }
      public hfmap Blur(int r) {
        hfmap m2=new hfmap(Width,Height);
       unsafe{ fixed(byte* pd=Data,md=m2.Data) {    
        for(int y=0;y<Height;y++) {
          int i0=y-r<0?0:y-r,i1=y+r>=Height?Height-1:y+r;
          for(int x=0;x<Width;x++) {
              int j0=x-r<0?0:x-r,j1=x+r>=Width?Width-1:x+r;
              int doffset=(Width-(j1-j0+1))*4;
              for(int k=0;k<4;k++) {
                int s=0;
                int offset=i0*Width*4+j0*4+k;
                for(int i=i0;i<=i1;i++,offset+=doffset)
                  for(int j=j0;j<=j1;j++,offset+=4)                   
                    s+=pd[offset];
                md[y*Width*4+x*4+k]=(byte)(s/((i1-i0+1)*(j1-j0+1)));
              }
          }
        }
       }}
        return m2;
      }
      int XInRange(int x) {return x<0?0:x>=Width?Width-1:x;}
      int YInRange(int y) {return y<0?0:y>=Height?Height-1:y;}
      public static int Dist2(int dx,int dy) { return (int)(Math.Sqrt(dx*dx+dy*dy));}
      public bool SnapPoint(int size,int x,int y,out int sx,out int sy) {
        sx=x;sy=y;  
        int k=4;          
        int d2=0,h2=0;
        //x=XInRange(x);y=YInRange(y);
        int x0=XInRange(x-size),x1=XInRange(x+size),y0=YInRange(y-size),y1=YInRange(y+size);        
        for(int yi=y0;yi<=y1;yi++)           
          for(int xi=x0;xi<=x1;xi++) {
            byte h=Data[4*yi*Width+4*xi+3];
            if(h==0) continue;
            int d=Dist2(xi-x,yi-y);
            if(h-k*d>h2-k*d2) {
              d2=d;h2=h;
              sx=xi;sy=yi;
            }
          }
        return sx>=0;              
      }
      public int GetHeight(int x,int y) {
        if(x<0||y<0||x>=Width||y>=Height) return -1;
        return Data[(y*Width+x)*4+3];
      }
      public void Mirror(bool vertical,byte[] draw) {
        int x,y;
        if(vertical) {
          for(y=0;y<Height-y-1;y++) {
            int h=(y*Width),g=(Height-y-1)*Width;
            for(x=0;x<Width;x++) {
              byte b;
              b=draw[g];draw[g]=draw[h];draw[h]=b;
              g++;h++;
            }
          }
        } else {
          for(x=0;x<Width-x-1;x++) {
            int h=x,g=(Width-x-1);
            for(y=0;y<Height;y++) {
              byte b;
              b=draw[g];draw[g++]=draw[h];draw[h++]=b;
              g+=Width-1;h+=Width-1;
            }
          }
        }
      }
      public void Mirror(bool vertical) {
        int x,y;
        if(vertical) {
          for(y=0;y<Height-y-1;y++) {
            int h=4*(y*Width),g=4*(Height-y-1)*Width;
            for(x=0;x<4*Width;x++) {
              byte b;
              b=Data[g];Data[g]=Data[h];Data[h]=b;
              g++;h++;
            }
          }
        } else {
          for(x=0;x<Width-x-1;x++) {
            int h=4*x,g=4*(Width-x-1);
            for(y=0;y<Height;y++) {
              byte b;
              b=Data[g];Data[g++]=Data[h];Data[h++]=b;
              b=Data[g];Data[g++]=Data[h];Data[h++]=b;
              b=Data[g];Data[g++]=Data[h];Data[h++]=b;
              b=Data[g];Data[g++]=Data[h];Data[h++]=b;
              g+=4*Width-4;h+=4*Width-4;
            }
          }
        }
      }
      public void Rotate90(bool right,byte[] draw) {
        if(draw==null) return;
        byte[] data=new byte[Width*Height];
        Array.Copy(draw,data,draw.Length);
        int d=Height;
        d=right?-d:d;
       unsafe {
        fixed(byte* pd2=draw,pd=data) {
          for(int y=0;y<Height;y++) {
            byte* g=(byte*)(pd2+(right?(Width-1)*Height+y:(Height-y-1))),h=(byte*)(pd+y*Width);
            for(int x=0;x<Width;x++) {              
              *g=*h++;
              g+=d;
            }
          }
       }}
      }
      public void Rotate90x4(bool right,byte[] draw) {
        if(draw==null) return;
        byte[] data=new byte[4*Width*Height];
        Array.Copy(draw,data,draw.Length);
        int d=Height;
        d=right?-d:d;
       unsafe {
        fixed(byte* pd2=draw,pd=data) {
          int* pd2i=(int*)pd2,pdi=(int*)pd;
          for(int y=0;y<Height;y++) {
            int* g=(int*)(pd2i+(right?(Width-1)*Height+y:(Height-y-1))),h=(int*)(pdi+y*Width);
            for(int x=0;x<Width;x++) {              
              *g=*h++;
              g+=d;
            }
          }
       }}
      }
      public hfmap Rotate90(bool right) {
        hfmap map2=new hfmap(Height,Width);
        int d=Height;
        d=right?-d:d;
       unsafe {
        fixed(byte* pd2=map2.Data,pd=Data) {
          for(int y=0;y<Height;y++) {
            int* g=(int*)(pd2+4*(right?(Width-1)*Height+y:(Height-y-1))),h=(int*)(pd+4*y*Width);
            for(int x=0;x<Width;x++) {              
              *g=*h++;
              g+=d;
            }
          }
       }}
        return map2;
      }
    }
    public class linear {
      byte[] XY;
      public linear(byte[] xy) {XY=xy;}
      public byte fx(byte x) {
        if(XY==null||XY.Length<1||0!=(XY.Length&1)) return 0;
        int i=0;
        while(i<XY.Length-4&&XY[i+2]<x) i+=2;
        if(x<=XY[i]) return XY[i+1];
        if(x>=XY[i+2]) return XY[i+3];
        double r=(x-XY[i])*1.0/(XY[i+2]-XY[i]);
        return (byte)(r*XY[i+3]+(1-r)*XY[i+1]);
      }
    }
    public class bezier {
      public double[] XY;
      public bezier(double[] xy) {XY=xy;}
      static public double ft(double[] xy,int i,double t) {
        double t1=1-t;
        return t1*t1*t1*xy[i]+3*t*t1*t1*xy[i+2]+3*t*t*t1*xy[i+4]+t*t*t*xy[i+6];          
      }
      public double fx(double x) {
        if(XY==null||XY.Length<1||0!=(XY.Length&1)) return 0;
        int i=0;
        while(i<XY.Length-13&&XY[i+6]<x) i+=6;
        if(x<=XY[i]) return XY[i+1];
        if(x>=XY[i+6]) return XY[i+7];
        double t=0,t1=0;
        double ti=0,xi=XY[i],ta=1,xa=XY[i+6];
        for(int l=0;l<8;l++) { 
          t=(ti+ta)/2; // binary 
          //t=(x-xi)/(xa-xi)*(ta-ti); // radix
          t1=1-t;
          double x2=t1*t1*t1*XY[i]+3*t*t1*t1*XY[i+2]+3*t*t*t1*XY[i+4]+t*t*t*XY[i+6];          
          if(x2>x) { ta=t;xa=x2;}
          if(x2<x) { ti=t;xi=x2;}
        }
        return t1*t1*t1*XY[i+1]+3*t*t1*t1*XY[i+3]+3*t*t*t1*XY[i+5]+t*t*t*XY[i+7];
      }
      public void Shape(byte[] shape) {
        for(int i=0;i<shape.Length;i++)
          shape[i]=(byte)(fx(i/255.0)*255.992);
      }
      public void Shape(double[] shape) {
        int n=shape.Length-1;
        for(int i=0;i<shape.Length;i++)
          shape[i]=fx(1.0*i/n);
      }

      public void Delete(int p) {
        if(XY.Length<14||p<0||p>XY.Length-8) return;
        double[] xy2=new double[XY.Length-6];
        for(int i=0;i<xy2.Length;i+=2) {
          xy2[i]=XY[i<p-2?i:i+6];
          xy2[i+1]=XY[i<p-2?i+1:i+7];
        }
        XY=xy2;
      }
      public void Insert(int p) {
        //double t=0.5;
        double[] xy2=new double[XY.Length+6];
        for(int i=0;i<XY.Length;i+=2) {
          xy2[i<p+4?i:i+6]=XY[i];
          xy2[i<p+4?i+1:i+7]=XY[i+1];
        }
        xy2[p+4]=ft(XY,p,0.25);
        xy2[p+5]=ft(XY,p+1,0.25);
        xy2[p+6]=ft(XY,p,0.5);
        xy2[p+7]=ft(XY,p+1,0.5);
        xy2[p+8]=ft(XY,p,0.75);
        xy2[p+9]=ft(XY,p+1,0.75);
        XY=xy2;
      }
    }
    public abstract class Common {
      public static int atoi(string s,int def) {
        int i;
        return int.TryParse(s,out i)?i:def;
      }
      public static double atof(string s) {
       try {
        return double.Parse(s.Replace(',','.'),System.Globalization.CultureInfo.InvariantCulture);
       } catch { return double.NaN;}
      }
      public static string ftoa(double x) {
        return x.ToString(System.Globalization.CultureInfo.InvariantCulture);
      } 
    }
    public class Pal {
        public static double max(double a,double b,double c) {
          return a>b?a>c?a:c:b>c?b:c;  
        }
        public static double size(double a,double b,double c) {
          return Math.Sqrt(a*a+b*b+c*c);
        }
        public static void Color(byte[] data,int offset,double value,double[] palette,bool hsv) {         
          int p;
          if(value<=palette[0]) {
            data[offset+2]=(byte)(palette[1]*255.5);
            data[offset+1]=(byte)(palette[2]*255.5);
            data[offset]=(byte)(palette[3]*255.5);
          } else if(value>=palette[palette.Length-4]) {
            p=palette.Length-3;
            data[offset+2]=(byte)(palette[p++]*255.5);
            data[offset+1]=(byte)(palette[p++]*255.5);
            data[offset]=(byte)(palette[p++]*255.5);
          } else {
            for(p=0;p<palette.Length&&value>palette[p+4];p+=4);
            double r1=(value-palette[p])/(palette[p+4]-palette[p]),r0=1-r1;
            if(hsv) {
              double r=palette[p+1]*r0+palette[p+5]*r1;
              double g=palette[p+2]*r0+palette[p+6]*r1;
              double b=palette[p+3]*r0+palette[p+7]*r1;
              double s=size(palette[p+1],palette[p+2],palette[p+3])*r0+size(palette[p+5],palette[p+6],palette[p+7])*r1;
              double s2=size(r,g,b);
              if(s2>0) {
                double m=max(r,g,b);
                s/=s2;
                if(m*s>1) s=1/m;
                r*=s;g*=s;b*=s;
              }
              data[offset+2]=(byte)(255.5*r);
              data[offset+1]=(byte)(255.5*g);
              data[offset]=(byte)(255.5*b);
            } else {
              data[offset+2]=(byte)(255.5*(palette[p+1]*r0+palette[p+5]*r1));
              data[offset+1]=(byte)(255.5*(palette[p+2]*r0+palette[p+6]*r1));
              data[offset]=(byte)(255.5*(palette[p+3]*r0+palette[p+7]*r1));
            }
          }
        }
        public static void RGB2HSV(double r,double g,double b,out double h,out double s,out double v) {
           double min=r<g?r<b?r:b:g<b?g:b;
           double max=r>g?r>b?r:b:g>b?g:b;
           v=max;
           if(max==0) {
             s=0;
             h=-1;
             return;
           }
           double delta=max-min;
           s=delta/max;
           if(r==max) h=(g-b)/delta;
           else if(g==max) h=2+(b-r)/delta;
           else h=4+(r-g)/delta;
           h*=60;
           if(h<0) h+=360;
        }
        public static void HSV2RGB(double h,double s,double v,out double r,out double g,out double b) {
          if(s==0) {
            r=g=b=s;
            return;
          }
          h/=60;
          int i=(int)Math.Floor(h);
          double f=h-i;
          double p=v*(1-s),q=v*(1-s*f),t=v*(1-s*(1-f));
          switch(i) {
           case 0:r=v;g=t;b=p;break;
           case 1:r=q;g=v;b=p;break;
           case 2:r=p;g=v;b=t;break;
           case 3:r=p;g=q;b=v;break;
           case 4:r=t;g=p;b=v;break;
           default:r=v;g=p;b=q;break;               
          }          
        }
        public static int ColorIntensity(int color,int i) {
          if(i==100) return color;
          if(i<=0) return 0;
          int r=color&255,g=(color>>8)&255,b=(color>>16)&255;
          r=r*i/256;if(r>255) r=255;
          g=g*i/256;if(g>255) g=255;
          b=b*i/256;if(b>255) b=255;
          return r|(g<<8)|(b<<16);
        }
        public const int White=0xffffff,Black=0;
        public static int RGBSum(int color) {
          return (color&255)+((color>>8)&255)+((color>>16)&255);
        }        
        public static int ColorIntensity765(int color,int i) {
          if(i<0) return Black;else if(i>765) return White;
          int r=color&255,g=(color>>8)&255,b=(color>>16)&255;
          int mi,ma,s=r+g+b;
          if(s==i) goto end;
          if(r==b&&b==g) { r=g=i/3;b=i-r-g;goto end;}          
          if(r<g) { mi=r;ma=g;} else {mi=g;ma=r;}
          if(b<mi) mi=b;else if(b>ma) ma=b;
          if(mi>0||ma<255) {
            int sr=(r-mi)*255/(ma-mi),sg=(g-mi)*255/(ma-mi),sb=(b-mi)*255/(ma-mi);
            int ss=r+g+b;
            if(i<s&&ss<s||i>s&&ss>s) {
              r=sr;g=sg;b=sb;s=ss;
            }
          }
          if(i<s) {
            r=r*i/s;g=g*i/s;b=i-r-g;
          } else {
            i=765-i;s=765-s;
            r=255-((255-r)*i/s);g=255-((255-g)*i/s);b=(765-i)-r-g;
          }
         end: 
          return r|(g<<8)|(b<<16);
        }
        public static Color IntColor(int rgb) {
          return System.Drawing.Color.FromArgb((255<<24)|rgb);
        }
        public static int IntColor(Color c) { return c.ToArgb()&0xffffff;}
        public static int MixColor(int c,int c2,int a,int n) {
          if(a<1) return c;else if(a>=n) return c2;
          int an=n-a;
          int b=(c&255)*an+(c2&255)*a,g=((c>>8)&255)*an+((c2>>8)&255)*a,r=((c>>16)&255)*an+((c2>>16)&255)*a;
          return (b/n)|((g/n)<<8)|((r/n)<<16);
        }
        public static int NegColor(int rgb) {
          int r=rgb&255,g=(rgb>>8)&255,b=(rgb>>16)&255,mi,ma;
          if(r<g) {mi=r;ma=g;} else {mi=g;ma=r;}
          if(b>mi) mi=b;else if(b>ma) ma=b;
          ma=255-ma;
          r=ma+r-mi;
          g=ma+g-mi;
          b=ma+b-mi;
          return r|(g<<8)|(b<<16);
        }
        public static void NegColor(byte[] data,int i) {
          byte r=data[i],g=data[i+1],b=data[i+2],mi,ma;
          if(r<g) {mi=r;ma=g;} else {mi=g;ma=r;}
          if(b<mi) mi=b;else if(b>ma) ma=b;
          ma=(byte)(255-ma);
          data[i]=(byte)(ma+r-mi);
          data[i+1]=(byte)(ma+g-mi);
          data[i+2]=(byte)(ma+b-mi);
        }        
        public static unsafe void NegColor(byte *data,int i) {
          byte r=data[i],g=data[i+1],b=data[i+2],mi,ma;
          if(r<g) {mi=r;ma=g;} else {mi=g;ma=r;}
          if(b<mi) mi=b;else if(b>ma) ma=b;
          ma=(byte)(255-ma);
          data[i]=(byte)(ma+r-mi);
          data[i+1]=(byte)(ma+g-mi);
          data[i+2]=(byte)(ma+b-mi);
        }
       public static unsafe void RotateColor(byte* p) {
         byte r=*p,g=p[1],b=p[2];
         *p=b;p[1]=r;p[2]=g;
       }
       public static unsafe void RGB2CMY(byte* p) {
          byte r=*p,g=p[1],b=p[2],mi,ma;
          if(r<g) {mi=r;ma=g;} else {mi=g;ma=r;}
          if(b<mi) mi=b;else if(b>ma) ma=b;          
          *p=(byte)(mi+ma-r);
          p[1]=(byte)(mi+ma-g);
          p[2]=(byte)(mi+ma-b);
        }
       public static unsafe void RXB(byte* p) {
         byte r=*p;
         *p=p[2];
         p[2]=r;
       }
    }

}
