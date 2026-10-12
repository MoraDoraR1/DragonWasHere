using System.Collections.Generic;
using UnityEngine;

namespace DWH.Data
{
    /// <summary>CSV 한 파일을 담는 테이블. CsvImporter(DWH/Import Data)가 채운다.</summary>
    public abstract class DataTable<TRow> : ScriptableObject
    {
        public List<TRow> rows = new();
    }
}
