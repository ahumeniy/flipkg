#!/bin/bash

FZBRANCH=${FZBRANCH:-"dev"}
TEMPAPPDIR=$(mktemp -d -t fap.XXXXXX)
WORK_DIR=${PWD}
PRESERVE=false
LOCAL=false
SOURCEDIR="/"

function usage() {
  EXITCODE=${1:-0}
  cat <<-EOF
Usage: ./build-fap.sh [options] repo_url [owner/id tag_name]
  
Options:
  repo_url              URL of the app repository
  owner/id              (Reserved) Upload info for the flipkg service.
  tag_name              (Reserved) Upload info for the flipkg service.
  -h                    shows usage
  -b branch             branch (or tag) of the app repo to build
  -B firmware_branch    branch (or tag) of the Flipper zero firmware to build for.
                        If there's already a firmware version installed, this argument
                        is ignored.
  -d source_dir         Directory within the app repo where the App code resides.
  -r                    clone submodules from app repo
  -n appid              override app id detection and use the specified app id instead
  -p                    preserve artifacts (don't cleanup)
  -P patch_in_b64_gz    Apply a patch encoded in base64 and gzipped
  -l                    local build (don't upload the results). Involves -p
                        When doing a local build, owner/id and tag_name are ignored.
EOF
  exit $((EXITCODE))
}

optstring=":hb:B:d:P:n:plr"

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
    d)
      SOURCEDIR="${OPTARG}"
      ;;
    p)
      PRESERVE=true
      ;;
    P)
      PATCH="${OPTARG}"
      ;;
    l)
      PRESERVE=true
      LOCAL=true
      ;;
    r)
      RECURSIVE="--recursive"
      ;;
    n)
      FAPNAME="${OPTARG}"
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

if [ -z $1 ]; then
  echo "Must provide the app source repo!"
  usage 64
fi

if [ "$LOCAL" != true ]; then
  if [[ ( -z $2 ) ]]; then
    echo "Must provide owner/id for this build! Are you trying to do a (-l)ocal build instead?"
    usage 64
  else
    OWNERID=$2
  fi

  if [[ ( -z $3 )]]; then
    echo "Must provide a tag name for this build! Are you trying to do a (-l)ocal build instead?"
    usage 64
  else
    BUILD_TAG=$3
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
  if [ -d ./flipper-base ]; then
    echo "Detected a flipper-base directory, copying into ${WORK_DIR}/flipper..."
    cp -r flipper-base flipper
  fi
  if [ ! -d ./flipper/.git ]; then
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
  git clone $SOURCEREPO $branchpart --single-branch $RECURSIVE $TEMPAPPDIR

  if [ ! -z $PATCH ]; then
    TEMPPATCHFN=$(mktemp -t XXXXXX.patch.gz)
    base64 -d <<< $PATCH > $TEMPPATCHFN
    gzip -df $TEMPPATCHFN
    cd $TEMPAPPDIR
    git apply ${TEMPPATCHFN%.gz}
    cd $WORK_DIR
  fi
}

# Extract the code folder, if the code is in a subfolder
function moveAppRepo {
  OLDTEMPAPPDIR=$TEMPAPPDIR
  TEMPAPPDIR="${TEMPAPPDIR}${SOURCEDIR}"
  TEMPAPPDIR="${TEMPAPPDIR%/}"
  echo "Moving ${TEMPAPPDIR} into ${WORK_DIR}/flipper/applications_user..."
  mv $TEMPAPPDIR ./flipper/applications_user
  TEMPAPPDIR=${TEMPAPPDIR##*/}
  TEMPAPPDIR="${PWD}/flipper/applications_user/${TEMPAPPDIR}"
  echo "Moved app directory to ${TEMPAPPDIR}."
  D_FAP_NAME=$(cat ${TEMPAPPDIR}/application.fam | grep appid | sed -e "s/[ \t]*appid=\"//" | sed -e "s/\",*//")
  FAP_CATEGORY=$(cat ${TEMPAPPDIR}/application.fam | grep fap_category | sed -e "s/[ \t]*fap_category=\"//" | sed -e "s/\",*//")
  echo "Detected FAP name ${D_FAP_NAME}"
  echo "FAP category ${FAP_CATEGORY}"

  if [ -z $FAPNAME ]; then
    FAPNAME=$D_FAP_NAME
    echo "FAPNAME $FAPNAME"
  fi
}

# Build the .fap
function buildFap {
  echo "Bulding the ${FAPNAME} app."
  LOGFILE="${TEMPAPPDIR##*/}_build.log"
  cd flipper
  ./fbt fap_${FAPNAME} 2>&1 | tee $LOGFILE
  FAPFILE=$(cat $LOGFILE | grep APPCHK | sed -e "s/^[ \t]*APPCHK[ \t]*//")
  echo "Fap generated as ${FAPFILE}."

  # Copy fap file to output
  mkdir -p $WORK_DIR/out/$FAP_CATEGORY
  cp $FAPFILE $WORK_DIR/out/$FAP_CATEGORY
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