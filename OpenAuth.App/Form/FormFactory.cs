using System;
using OpenAuth.Repository.Domain;
using Infrastructure;
using SqlSugar;

namespace OpenAuth.App
{
    public class FormFactory
    {
        public static IForm CreateForm(Form form, ISqlSugarClient sugarClient)
        {
            if (form.FrmType == Define.FORM_TYPE_DYNAMIC)
            {
                return new LeipiForm(sugarClient);
            }else if (form.FrmType == Define.FORM_TYPE_DEVELOP)
            {
                throw new Exception("自定义表单不需要创建数据库表");
            }
            else 
            {
                return new DragForm(sugarClient);
            }
        }
    }
}