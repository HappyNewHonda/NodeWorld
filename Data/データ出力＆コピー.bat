cd %~dp0
python generate.py
copy json\*.* ..\Assets\Scripts\Data
copy id\*.* ..\Assets\Scripts\Data
