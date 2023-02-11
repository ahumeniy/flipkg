#!/bin/bash

FZBRANCH=${FZBRANCH:-"dev"}
TEMPAPPDIR=$(mktemp -d -t fap.XXXXXX)
WORK_DIR=${PWD##*/}
PRESERVE=false
LOCAL=false

function usage() {
  EXITCODE=${1:-0}
  cat <<-EOF
Usage: ./build-fap.sh [options] repo_url fap_name owner/id tag_name
  
Options:
  -h                    shows usage
  -b branch             branch (or tag) of the app repo to build
  -B firmware_branch    branch (or tag) of the Flipper zero firmware to build for.
                        If there's already a firmware version installed, this argument
                        is ignored.
  -p                    preserve artifacts (don't cleanup)
  -l                    local build (don't upload the results). Involves -p
                        When doing a local build, owner/id and tag_name are ignored.
EOF
  exit $((EXITCODE))
}

optstring=":hb:B:pl"

while getopts ${optstring} arg; do
  case ${arg} in
    h)
      usage
      ;;
    b)
      BRANCH="${OPTARG}"
      ;;
    B)
      FZBRANCH="${OPTARG}"
      ;;
    p)
      PRESERVE=true
      ;;
    l)
      PRESERVE=true
      LOCAL=true
      ;;
    :)
      echo "$0: must supply an argument to -$OPTARG." >&2
      exit 1
      ;;
    ?)
      echo "Invalid option: -${OPTARG}."
      exit 2
      ;;
  esac
done

shift $((OPTIND -1))

SOURCEREPO=$1
FAPNAME=$2

if [ -z $1 ]; then
  echo "Must provide the app source repo!"
  usage 64
fi

if [ -z $2 ]; then
  echo "Must provide the app name to build!"
  usage 64
fi

if [ "$LOCAL" != true ]; then
  if [[ ( -z $3 ) ]]; then
    echo "Must provide owner/id for this build! Are you trying to do a (-l)ocal build instead?"
    usage 64
  else
    OWNERID=$3
  fi

  if [[ ( -z $4 )]]; then
    echo "Must provide a tag name for this build! Are you trying to do a (-l)ocal build instead?"
    usage 64
  else
    BUILD_TAG=$4
  fi

  if [[(-z $AZURE_STORAGE_CONTAINER)]]; then
    echo "AZURE_STORAGE_CONTAINER not set! Are you trying to do a (-l)ocal build instead?"
    usage 78
  fi

  if [[(-z $AZURE_STORAGE_TOKEN)]]; then
    echo "AZURE_STORAGE_TOKEN not set! Are you trying to do a (-l)ocal build instead?"
    usage 78
  fi
fi

echo "Source repo ${SOURCEREPO}"

# Find the specific firmware version or download it.
function getFlipperRepo {
  if [ ! -d ./flipper ]; then
    echo "clonning ${FZBRANCH} from flipperzero-firmware repo"
    git clone https://github.com/flipperdevices/flipperzero-firmware.git -b $FZBRANCH --single-branch flipper
  else
    echo "Flipper repo already exists. Using existing..."
  fi
}

# Download the app repo
function getAppRepo {
  echo "Clonning ${SOURCEREPO} into ${TEMPAPPDIR}..."
  branchpart=""
  if [ ! -z $BRANCH ]; then
    branchpart="-b ${BRANCH}"
  fi
  git clone $SOURCEREPO $branchpart --single-branch $TEMPAPPDIR
}

# Extract the code folder, if the code is in a subfolder
function moveAppRepo {
  # TODO: Move subfolder if the option is enabled
  mv $TEMPAPPDIR ./flipper/applications_user/
  OLDTEMPAPPDIR=$TEMPAPPDIR
  TEMPAPPDIR=${TEMPAPPDIR##*/}
  TEMPAPPDIR="${PWD}/flipper/applications_user/${TEMPAPPDIR}"
  echo "Moved app directory to ${TEMPAPPDIR}."
}

# Build the .fap
function buildFap {
  echo "Bulding the ${FAPNAME} app."
  LOGFILE="${TEMPAPPDIR##*/}_build.log"
  cd flipper
  ./fbt fap_${FAPNAME} 2>&1 | tee $LOGFILE
  FAPFILE=$(cat $LOGFILE | grep APPCHK | sed -e "s/^[ \t]*APPCHK[ \t]*//")
  echo "Fap generated as ${FAPFILE}."
}

# Export the result
function exportToAZStorage {
  if [ "$LOCAL" != true ]; then
    FAPFILENAME=$(basename -- "$FAPFILE");
    FAPDESTINATION="${AZURE_STORAGE_CONTAINER}/${OWNERID}/${BUILD_TAG}/Official/${FZBRANCH}/${FAPFILENAME}"
    echo "uploading file to Azure blob storage"
    export AZCOPY_CRED_TYPE=Anonymous;
    export AZCOPY_CONCURRENCY_VALUE=AUTO;
    azcopy copy ${FAPFILE} "${FAPDESTINATION}?${AZURE_STORAGE_TOKEN}" --from-to=LocalBlob --blob-type BlockBlob --put-md5 --log-level=INFO;
    unset AZCOPY_CRED_TYPE;
    unset AZCOPY_CONCURRENCY_VALUE;

    echo "File stored at ${FAPDESTINATION}"
  fi
}

# Cleanup
function cleanup {
  if [ "$PRESERVE" = false ]; then
    rm -rf $OLDTEMPAPPDIR
    rm -rf $TEMPAPPDIR
    rm $FAPFILE
  fi
}

getFlipperRepo
getAppRepo
moveAppRepo
buildFap
exportToAZStorage
cleanup