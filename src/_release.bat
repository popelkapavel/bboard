set VER=015
del www\bboard%VER%.zip
del www\bboard%VER%_src.zip
cd bin
7z a -tzip ..\www\bboard%VER%.zip bboard.exe ..\bboard.rtf
@rem mesh.dll ..\cubes.rtf ..\cubes.ini
cd ..
7z a -tzip www\bboard%VER%_src.zip *.* Properties img -xr!.vs
copy bboard.rtf www

