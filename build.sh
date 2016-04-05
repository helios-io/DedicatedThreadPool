#!/bin/bash

SCRIPT_PATH="${BASH_SOURCE[0]}";
if ([ -h "${SCRIPT_PATH}" ]) then
  while([ -h "${SCRIPT_PATH}" ]) do SCRIPT_PATH=`readlink "${SCRIPT_PATH}"`; done
fi
pushd . > /dev/null
cd `dirname ${SCRIPT_PATH}` > /dev/null
SCRIPT_PATH=`pwd`;
popd  > /dev/null

mono $SCRIPT_PATH/tools/nuget/NuGet.exe update -self

mono $SCRIPT_PATH/tools/nuget/NuGet.exe install FAKE -OutputDirectory $SCRIPT_PATH/src/packages -ExcludeVersion

mono $SCRIPT_PATH/tools/nuget/NuGet.exe install NBench.Runner -OutputDirectory $SCRIPT_PATH/src/packages -ExcludeVersion -Version 0.1.6


if ! [ -e $SCRIPT_PATH/src/packages/SourceLink.Fake/tools/SourceLink.fsx ] ; then
    mono $SCRIPT_PATH/tools/nuget/NuGet.exe install SourceLink.Fake -OutputDirectory $SCRIPT_PATH/src/packages -ExcludeVersion -Version 0.1.6

fi

export encoding=utf-8

mono $SCRIPT_PATH/src/packages/FAKE/tools/FAKE.exe build.fsx "$@"
