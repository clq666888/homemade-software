using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;

namespace LocalConverter
{
    public sealed partial class ImageWorkspace
    {
        string textUpdateId;
        int textPreviewGeneration;
        bool stoppingTextPreview;
        Task textPreview=Task.FromResult(0);

        void DeleteSelectedText()
        {
            if(Busy||selectedTextOriginal==null||canvas.SelectedTextId==null)return;
            FlushText(false);
            var edit=new ImageEdit{Kind="removeText",TargetId=selectedTextOriginal.Id};
            ClearTextSelection();CommitEdit(edit);BuildOptions();
        }

        void ScheduleTextPreview()
        {
            textPreviewGeneration++;
            if(textPreview.IsCompleted&&!Busy&&!stoppingTextPreview)textPreview=RenderTextPreview();
        }

        // A single background renderer consumes the latest immutable snapshot. Never
        // disable/rebuild the text box here: that interrupts typing and IME composition.
        async Task RenderTextPreview()
        {
            int renderedGeneration;
            do
            {
                renderedGeneration=textPreviewGeneration;
                var asset=Selected;
                if(Busy||IsDisposed||stoppingTextPreview||mode!="图片编辑"||viewingResult||asset==null)return;
                var edits=asset.Edits.ToArray();int version=revision;
                try
                {
                    var result=await Task.Run(()=>ImageEditing.RenderLayers(asset.Prepared,edits,false));
                    using(result)
                    {
                        if(Busy||IsDisposed||stoppingTextPreview||mode!="图片编辑"||viewingResult||Selected!=asset||canvas.Capture)return;
                        if(renderedGeneration!=textPreviewGeneration||version!=revision)continue;
                        canvas.SetImage(result.Picture,false);result.Picture=null;
                        canvas.TextTargets.Clear();canvas.TextTargets.AddRange(ImageEditing.TextTargets(edits,new Size(asset.Width,asset.Height)));
                        undo.Enabled=asset.Edits.Count>0;redo.Enabled=asset.Redo.Count>0;
                        status.Text="文字已实时更新 · 连续输入可一次撤销 · 完成后生成结果。";
                        RefreshResult();
                    }
                }
                catch(Exception error)
                {
                    if(!IsDisposed&&!Busy&&renderedGeneration==textPreviewGeneration)status.Text="文字预览失败："+error.Message+"。可重试或撤销。";
                }
            }while(renderedGeneration!=textPreviewGeneration);
        }
    }
}
