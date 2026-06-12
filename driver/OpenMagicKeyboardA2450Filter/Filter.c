/*
 * Filter.c — HID report interception and transformation.
 *
 * Design / build skeleton only.
 * Do not install.  Do not bind to real hardware.
 *
 * This file implements the core filter logic that intercepts
 * IOCTL_HID_READ_REPORT and transforms A2450 keyboard reports
 * in the completion routine.
 *
 * IRQL / concurrency notes:
 *   - Completion routine may run at IRQL <= DISPATCH_LEVEL.
 *   - A2450TransformKeyboardReport only does byte manipulation,
 *     no memory allocation, no blocking — safe at any IRQL.
 *   - No spinlock added in this phase.
 *   - TransformState currently holds per-report computation state
 *     and configuration switches only.
 *   - If cross-report state is introduced later, concurrency
 *     protection must be re-evaluated.
 */

#include <ntddk.h>
#include <wdf.h>
#include <hidport.h>
#include "Device.h"
#include "ReportTransform.h"
#include "A2450Report.h"

/* Forward declarations */
EVT_WDF_REQUEST_COMPLETION_ROUTINE A2450FilterReadReportCompletion;

/*
 * A2450FilterEvtIoInternalDeviceControl — intercepts HID IOCTLs.
 *
 * For IOCTL_HID_READ_REPORT: set completion routine and forward.
 * For all other IOCTLs: forward directly without completion routine.
 *
 * Any failure in format/send is completed back to the caller immediately.
 */
VOID
A2450FilterEvtIoInternalDeviceControl(
    _In_ WDFQUEUE Queue,
    _In_ WDFREQUEST Request,
    _In_ size_t OutputBufferLength,
    _In_ size_t InputBufferLength,
    _In_ ULONG IoControlCode
)
{
    PA2450_DEVICE_CONTEXT ctx;
    NTSTATUS status;
    BOOLEAN sent;
    WDF_REQUEST_SEND_OPTIONS options;

    UNREFERENCED_PARAMETER(OutputBufferLength);
    UNREFERENCED_PARAMETER(InputBufferLength);

    ctx = A2450GetDeviceContext(WdfIoQueueGetDevice(Queue));

    if (IoControlCode == IOCTL_HID_READ_REPORT)
    {
        WdfRequestFormatRequestUsingCurrentType(Request);

        WdfRequestSetCompletionRoutine(
            Request,
            A2450FilterReadReportCompletion,
            (WDFCONTEXT)ctx
        );

        sent = WdfRequestSend(
            Request,
            ctx->IoTarget,
            WDF_NO_SEND_OPTIONS
        );

        if (!sent)
        {
            status = WdfRequestGetStatus(Request);
            WdfRequestComplete(Request, status);
        }

        return;
    }

    /*
     * Non-READ_REPORT IOCTLs: fire-and-forget, no completion routine.
     * Use SEND_AND_FORGET so the framework does not expect us to
     * reclaim the request on a completion path we do not own.
     */
    WdfRequestFormatRequestUsingCurrentType(Request);

    WDF_REQUEST_SEND_OPTIONS_INIT(
        &options,
        WDF_REQUEST_SEND_OPTION_SEND_AND_FORGET
    );

    sent = WdfRequestSend(
        Request,
        ctx->IoTarget,
        &options
    );

    if (!sent)
    {
        status = WdfRequestGetStatus(Request);
        WdfRequestComplete(Request, status);
    }
}

/*
 * A2450FilterReadReportCompletion — called when the lower driver completes
 * an IOCTL_HID_READ_REPORT request.
 *
 * If the report is a 10-byte A2450 keyboard report (ID 0x01),
 * apply A2450TransformKeyboardReport in-place.
 * On any failure, pass the report through unmodified.
 */
VOID
A2450FilterReadReportCompletion(
    _In_ WDFREQUEST Request,
    _In_ WDFIOTARGET Target,
    _In_ PWDF_REQUEST_COMPLETION_PARAMS Params,
    _In_ WDFCONTEXT Context
)
{
    PA2450_DEVICE_CONTEXT ctx = (PA2450_DEVICE_CONTEXT)Context;
    WDFMEMORY memory = NULL;
    PVOID buffer = NULL;
    size_t length = 0;
    size_t bytesReturned;
    NTSTATUS status;

    UNREFERENCED_PARAMETER(Target);

    if (!NT_SUCCESS(Params->IoStatus.Status))
    {
        /* Lower driver failed — pass through the failure as-is. */
        WdfRequestCompleteWithInformation(
            Request,
            Params->IoStatus.Status,
            Params->IoStatus.Information
        );
        return;
    }

    /*
     * MVP-A only processes the confirmed 10-byte A2450 keyboard report.
     * If the lower driver returns anything other than exactly 10 bytes,
     * pass through unchanged. This avoids accidentally modifying other
     * reports or future extended report formats.
     */
    bytesReturned = (size_t)Params->IoStatus.Information;

    if (bytesReturned != A2450_REPORT_LENGTH)
    {
        ctx->ReportsPassedThrough++;
        WdfRequestCompleteWithInformation(
            Request,
            Params->IoStatus.Status,
            Params->IoStatus.Information
        );
        return;
    }

    status = WdfRequestRetrieveOutputMemory(Request, &memory);

    if (!NT_SUCCESS(status))
    {
        /* Cannot get output buffer — pass through unchanged. */
        ctx->ReportsPassedThrough++;
        WdfRequestCompleteWithInformation(
            Request,
            Params->IoStatus.Status,
            Params->IoStatus.Information
        );
        return;
    }

    buffer = WdfMemoryGetBuffer(memory, &length);

    if (buffer == NULL || !A2450IsKeyboardReport((const UCHAR*)buffer, length))
    {
        /* Not a keyboard report or null buffer — pass through. */
        ctx->ReportsPassedThrough++;
        WdfRequestCompleteWithInformation(
            Request,
            Params->IoStatus.Status,
            Params->IoStatus.Information
        );
        return;
    }

    /*
     * Transform the 10-byte A2450 keyboard report in-place.
     * A2450TransformKeyboardReport validates length and report ID
     * internally, so this is a safe double-check.
     */
    if (A2450TransformKeyboardReport((UCHAR*)buffer, length, &ctx->TransformState))
    {
        ctx->ReportsTransformed++;
    }
    else
    {
        ctx->ReportsPassedThrough++;
    }

    WdfRequestCompleteWithInformation(
        Request,
        Params->IoStatus.Status,
        Params->IoStatus.Information
    );
}
