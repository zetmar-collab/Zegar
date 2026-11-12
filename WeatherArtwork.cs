using System;
using System.Windows;
using System.Windows.Media;

namespace Zegar;

internal static class WeatherArtwork
{
    public static ImageSource Create(int code,bool day,Color? monochrome=null)
    {
        var drawing=new DrawingGroup();
        using(var dc=drawing.Open())
        {
            // A fixed view box prevents the layout from moving between conditions.
            dc.DrawRectangle(Brushes.Transparent,null,new Rect(0,0,72,68));
            Brush ink=new SolidColorBrush(monochrome??Color.FromRgb(115,175,215));
            Brush cloud=new SolidColorBrush(monochrome??Color.FromRgb(190,217,238));
            Brush sun=new SolidColorBrush(monochrome??Color.FromRgb(246,188,73));
            void Sun(Point center,double radius)
            {
                var pen=new Pen(sun,2.5){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};
                for(int i=0;i<8;i++){double angle=i*Math.PI/4;dc.DrawLine(pen,new Point(center.X+Math.Cos(angle)*(radius+5),center.Y+Math.Sin(angle)*(radius+5)),new Point(center.X+Math.Cos(angle)*(radius+10),center.Y+Math.Sin(angle)*(radius+10)));}
                dc.DrawEllipse(sun,null,center,radius,radius);
            }
            if(code is 0 or 1)
            {
                if(day)Sun(new Point(36,32),14);
                else dc.DrawGeometry(sun,null,new CombinedGeometry(GeometryCombineMode.Exclude,new EllipseGeometry(new Point(34,30),20,20),new EllipseGeometry(new Point(43,22),18,18)));
            }
            else
            {
                if(code==2&&day)Sun(new Point(48,20),9);
                dc.DrawGeometry(cloud,new Pen(ink,1.5),Geometry.Parse("M18,47 C4,47 4,29 17,28 C18,10 43,8 49,26 C66,22 73,47 56,47 Z"));
                var pen=new Pen(ink,2.5){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};
                if(code is 45 or 48)for(int i=0;i<2;i++)dc.DrawLine(pen,new Point(17,53+i*7),new Point(55,53+i*7));
                else if(code is 95 or 96 or 99)dc.DrawGeometry(sun,null,Geometry.Parse("M37,43 L26,58 L35,58 L31,67 L48,51 L39,51 L45,43 Z"));
                else if(code is >=71 and <=77 or 85 or 86)
                    for(int i=0;i<3;i++){double x=23+i*13;dc.DrawLine(pen,new Point(x,53),new Point(x,63));dc.DrawLine(pen,new Point(x-4,55),new Point(x+4,61));dc.DrawLine(pen,new Point(x-4,61),new Point(x+4,55));}
                else if(code is 51 or 53 or 55 or 56 or 57 or 61 or 63 or 65 or 66 or 67 or 80 or 81 or 82)
                    for(int i=0;i<3;i++)dc.DrawLine(pen,new Point(25+i*13,53),new Point(20+i*13,62));
            }
        }
        drawing.Freeze();return new DrawingImage(drawing);
    }
}
