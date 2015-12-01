using System;
using System.Collections.Generic;
using System.Text;

namespace System.Collections.Generic
{
    /// <summary>
    /// MonoTODO待实现属性
    /// </summary>
    public class MonoTODO : Attribute
    {
        public string Desc;
        public MonoTODO() {
            Desc = "";
        }
        public MonoTODO(string desc) {
            Desc = desc;
        }
    }
    /// <summary>
    /// 属性限制条件
    /// </summary>
    public class MonoLimitation: Attribute
    {
        public string Desc;
        public MonoLimitation(string desc) {
            Desc = desc;
        }
    }
}
