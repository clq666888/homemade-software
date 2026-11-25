using System.Collections.Generic;
using System.Text;

namespace LocalConverter
{
    public static class OcrTextFlow
    {
        static bool Chinese(char c){return c>='\u3400'&&c<='\u9fff';}
        public static string Join(IEnumerable<ImageWord> words,bool keepLineBreaks)
        {
            var text=new StringBuilder();
            foreach(var word in words)
            {
                string value=word.Text==null?"":word.Text.Trim();if(value.Length==0)continue;
                if(text.Length>0)
                {
                    char last=text[text.Length-1],first=value[0];
                    if(keepLineBreaks&&word.NewLine)text.Append("\r\n");
                    else if(!(Chinese(last)&&Chinese(first))&&",.;:!?%)]，。；：！？、）】》”’".IndexOf(first)<0&&"-([{（【《“‘".IndexOf(last)<0)text.Append(' ');
                }
                text.Append(value);
            }
            return text.ToString();
        }
    }
}
