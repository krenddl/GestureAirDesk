using GestureFlow.Core;
internal static class ProfileChecks
{
    public static void Run(){
        string path=Path.Combine(Path.GetTempPath(),"GestureFlow-profiles-"+Guid.NewGuid()+".json");
        try{
            var store=new ControlProfileStore(path);
            if(store.Load().Count!=0)throw new Exception("Начальный список.");
            var first=new AppSettings(ProfileName:"Точный",TrackingPresetIndex:1,ClickMode:3,PointerInsetX:.2);
            var second=new AppSettings(ProfileName:"Быстрый",TrackingPresetIndex:2,WheelEnabled:true,ClickTrigger:1);
            store.Save([first,second]);
            if(!store.Load().SequenceEqual(new[]{first,second}))throw new Exception("Профили не восстановились.");
            if(store.Load()[1].ClickTrigger!=1)throw new Exception("Способ клика не восстановлен из файла.");
            try{store.Save([second with {ClickTrigger=-1}]);throw new Exception("Неверный способ клика разрешён.");}catch(InvalidDataException){}
            store.Save([second]);
            if(store.Load().Count!=1)throw new Exception("Замена набора.");
            try{store.Save([first,first with {ProfileName="ТОЧНЫЙ"}]);throw new Exception("Дубликат разрешён.");}catch(InvalidDataException){}
            try{store.Save([first with {ClickMode=4}]);throw new Exception("Неверный клик разрешён.");}catch(InvalidDataException){}
            try{store.Save(Enumerable.Range(0,21).Select(i=>first with{ProfileName="p"+i}).ToArray());throw new Exception("Лимит не работает.");}catch(InvalidDataException){}
            File.WriteAllText(path,"invalid");
            try{store.Load();throw new Exception("Повреждение не обнаружено.");}catch(System.Text.Json.JsonException){}
            Console.WriteLine("PASS: профили — два набора, восстановление, замена, дубликаты, валидация, лимит, повреждение.");
        }finally{File.Delete(path);File.Delete(path+".tmp");}
    }
}


