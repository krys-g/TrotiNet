#MSBUILD:=msbuild.exe
MSBUILD:=xbuild
MSBUILD_OPT:=/nologo /verbosity:quiet
BUILD_OPT:=/p:Platform="Any CPU" /p:Configuration=Release
NUNIT:=/cygdrive/c/Program Files/NUnit 2.6/bin/nunit-console-x86.exe
VERSION:=0.8.0

compile:
	@"${MSBUILD}" ${MSBUILD_OPT} ${BUILD_OPT}

clean:
	@find -depth -type d -name bin -exec rm -fr {} \;
	@find -depth -type d -name obj -exec rm -fr {} \;

release: set_version compile
	@mkdir -p Release/TrotiNet-${VERSION}
	@cp Lib/bin/Release/log4net.dll Lib/bin/Release/TrotiNet.dll \
	    Lib/bin/Release/TrotiNet.xml Release/TrotiNet-${VERSION}/
	@cd Release && zip -r TrotiNet-${VERSION}.zip TrotiNet-${VERSION}
	@rm -fr Release/TrotiNet-${VERSION}
	@echo "Created Release/TrotiNet-${VERSION}.zip"

set_version:
	@echo "Setting assemblies' version to ${VERSION}"
	@find -name AssemblyInfo.cs -exec perl -pi -e 's!(assembly: AssemblyVersion\(\")([0-9\.]+)\"!$${1}'"${VERSION}\"!" {} \;
	@find -type f -name AssemblyInfo.cs.bak -exec rm {} \;

test: compile
	@"${NUNIT}" Test\\bin\\Release\\TrotiNet.Test.dll
	@rm TestResult.xml

.PHONY: compile clean distclean release
