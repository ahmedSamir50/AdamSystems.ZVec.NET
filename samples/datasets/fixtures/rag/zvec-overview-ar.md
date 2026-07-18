# نظرة عامة على ZVec.NET

ZVec.NET هو حزمة SDK لـ .NET مبنية فوق Alibaba ZVec، قاعدة بيانات متجهات مضمّنة تُشبَّه غالبًا بـ SQLite لقواعد المتجهات.

تدعم الفهارس مثل HNSW، والـ ODM المكتوب بأنواع عبر سمات ZVec.NET.Mapping، والتسجيل في DI عبر AddZVec و AddZVecCollection.

تخزّن المجموعات تضمينات كثيفة كـ ReadOnlyMemory من نوع float. Dispose يغلق المجموعة؛ Destroy يحذف البيانات من القرص.

تطبيقات الحافة والعمل دون اتصال يمكنها حفظ البيانات تحت AppData مع تفعيل mmap لـ RAG والبحث الدلالي محليًا.
