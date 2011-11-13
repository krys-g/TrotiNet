MSBUILD:=msbuild.exe
MSBUILD_OPT:=/nologo /verbosity:quiet
BUILD_OPT:=/p:Platform="Any CPU" /p:Configuration=Release
NUNIT:=/cygdrive/c/Program Files/NUnit 2.5.10/bin/net-2.0/nunit-console-x86.exe
VERSION:=0.6.0

compile: 
	@"${MSBUILD}" ${MSBUILD_OPT} ${BUILD_OPT}

clean:
	@find -depth -type d -name bin -exec rm -fr {} \;
	@find -depth -type d -name obj -exec rm -fr {} \;

release: set_version compile
	@mkdir -p Package/TrotiNet-${VERSION}
	@cp Test/bin/Release/{log4net.dll,TrotiNet.{dll,xml}} \
          Package/TrotiNet-${VERSION}/
	@cd Package && zip -r TrotiNet-${VERSION}.zip TrotiNet-${VERSION}
	@rm -fr Package/TrotiNet-${VERSION}
	@echo "Created Package/TrotiNet-${VERSION}.zip"

set_version:
	@echo "Setting assemblies' version to ${VERSION}"
	@find -name AssemblyInfo.cs -exec perl -pi -e 's!(assembly: AssemblyVersion\(\")([0-9\.]+)\"!$${1}'"${VERSION}\"!" {} \;
	@find -type f -name AssemblyInfo.cs.bak -exec rm {} \;

test: compile
	@"${NUNIT}" Test\\bin\\Release\\TrotiNet.Test.dll
	@rm TestResult.xml

.PHONY: compile clean distclean release        
