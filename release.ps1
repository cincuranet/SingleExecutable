Push-Location
try {
	& msbuild SingleExecutable\SingleExecutable.csproj /p:Configuration=Release /t:Rebuild /v:m
	cd SingleExecutable\bin\Release
	& .\SingleExecutable.exe -e SingleExecutable.exe -o SingleExecutable.out
	rm -Force * -Exclude SingleExecutable.out
	mv SingleExecutable.out SingleExecutable.exe
}
finally {
	Pop-Location
}