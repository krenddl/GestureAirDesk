using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
namespace GestureFlow.Desktop;

// Offline illustration only; never used as recognition output.
sealed class HandGuide : Control
{
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var scale=Math.Min(Bounds.Width/200,Bounds.Height/260);
        Point P(double x,double y)=>new(x*scale+(Bounds.Width-200*scale)/2,y*scale);
        var lines=new[]
        {
            new[]{(92d,248d),(83d,203d),(56d,169d),(29d,144d),(14d,119d)},
            new[]{(83d,203d),(68d,127d),(59d,83d),(54d,48d),(53d,22d)},
            new[]{(83d,203d),(99d,115d),(98d,67d),(97d,28d),(96d,6d)},
            new[]{(83d,203d),(126d,124d),(133d,79d),(139d,46d),(143d,25d)},
            new[]{(83d,203d),(149d,144d),(164d,111d),(178d,85d),(187d,68d)},
            new[]{(68d,127d),(99d,115d),(126d,124d),(149d,144d)},
            new[]{(92d,248d),(126d,245d),(141d,204d),(149d,144d)}
        };
        foreach(var points in lines)
        for(int i=1;i<points.Length;i++)
        {
            var from=P(points[i-1].Item1,points[i-1].Item2);var to=P(points[i].Item1,points[i].Item2);
            context.DrawLine(new Pen(Brush.Parse("#185E687A"),13*scale),from,to);
            context.DrawLine(new Pen(Brush.Parse("#507F899A"),1*scale),from,to);
        }
        foreach(var points in lines.Take(5))
        foreach(var (x,y) in points.Skip(1))
            context.DrawEllipse(Brush.Parse("#BBC3CF"),null,P(x,y),2.3*scale,2.3*scale);
    }
}
