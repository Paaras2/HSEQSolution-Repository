using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HSEQ.Common
{
    // دسته‌بندی سند: ستاد (بدون پروژه، پیش‌فرض) یا پروژه. مقدار پیش‌فرض عمداً Headquarters
    // است (enum default = 0) تا فرم بدون انتخاب کاربر روی «ستاد» بیفتد.
    public enum DocumentCategory
    {
        Headquarters = 0,
        Project = 1
    }

    public enum DocumentVersion
    {
        A = 1,
        B = 2,
        C = 3,
        D = 4,
        E = 5,
        F = 6,
        G = 7,
        H = 8,
        I = 9,
        J = 10,
        K = 11,
        L = 12,
        M = 13,
        N = 14,
        O = 15,
        P = 16,
        Q = 17,
        R = 18,
        S = 19,
        T = 20,
        U = 21,
        V = 22,
        W = 23,
        X = 24,
        Y = 25,
        Z = 26
    }

    // نوع ارتباط میان دو مدرکِ متفاوت. جهت‌دار ذخیره می‌شود (مبدأ ← مقصد) و در نمایش،
    // سمت مقابل برچسب معکوس می‌گیرد - مثلاً «مرجع» از سمت مقصد یعنی «مورد استناد در».
    //
    // این را با Document.RelatedDocumentId اشتباه نگیرید: آن فیلد زنجیره‌ی بازنگری
    // (نسخه‌ی قبلیِ خودِ همین مدرک) است، نه ارتباط میان دو مدرک مستقل.
    public enum DocumentRelationType
    {
        // متقارن و بدون جهت - از هر دو سمت یک معنی می‌دهد.
        Related = 0,

        // مبدأ به مقصد استناد می‌کند؛ مقصد مدرک بالادستی/مرجع است.
        Reference = 1,

        // مقصد پیوست یا فرمِ مبدأ است.
        Attachment = 2,

        // مبدأ جایگزین مقصد شده است - مدرکی با شماره‌ی متفاوت، نه بازنگری همان مدرک.
        Replaces = 3
    }
}
